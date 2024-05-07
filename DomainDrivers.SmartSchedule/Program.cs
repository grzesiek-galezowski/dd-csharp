using System.Data;
using System.Text.Json;
using DomainDrivers.SmartSchedule.Allocation;
using DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;
using DomainDrivers.SmartSchedule.Allocation.Cashflow;
using DomainDrivers.SmartSchedule.Availability;
using DomainDrivers.SmartSchedule.Planning;
using DomainDrivers.SmartSchedule.Resource;
using DomainDrivers.SmartSchedule.Resource.Device;
using DomainDrivers.SmartSchedule.Resource.Employee;
using DomainDrivers.SmartSchedule.Risk;
using DomainDrivers.SmartSchedule.Shared;
using DomainDrivers.SmartSchedule.Simulation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Quartz;
using StackExchange.Redis;

namespace DomainDrivers.SmartSchedule;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddTestConfiguration();
        var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
        builder.Services.AddSingleton<IConnectionMultiplexer>(x =>
            x.GetRequiredService<Root>().RedisConnectionMultiplexer);

        ArgumentNullException.ThrowIfNull(postgresConnectionString);
        ArgumentNullException.ThrowIfNull(redisConnectionString);

        var dataSource = new NpgsqlDataSourceBuilder(postgresConnectionString)
            .ConfigureJsonOptions(new JsonSerializerOptions
            {
                IgnoreReadOnlyProperties = true, IgnoreReadOnlyFields = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })
            .EnableDynamicJson()
            .Build();
        builder.Services.AddDbContext<SmartScheduleDbContext>(options => { options.UseNpgsql(dataSource); });
        builder.Services.AddScoped<IDbConnection>(x =>
            x.GetRequiredService<SmartScheduleDbContext>().Database.GetDbConnection());

//shared
        builder.Services.AddSingleton<Root>(x =>
            new Root(redisConnectionString!, postgresConnectionString!));
//TimeProvider and EventsPublisher must be in container
        builder.Services.AddSingleton<TimeProvider>(x => x.GetRequiredService<Root>().CreateTimeProvider());
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
        builder.Services.AddScoped<IEventsPublisher, EventsPublisher>(
            x => x.GetRequiredService<Root>().CreateEventsPublisher(x.GetRequiredService<IMediator>()));
        //unit of work can be moved inside the root later:
        builder.Services.AddTransient<IUnitOfWork, UnitOfWork>(x =>
            x.GetRequiredService<Root>().CreateUnitOfWork(x.GetRequiredService<SmartScheduleDbContext>()));

// planning
        builder.Services.AddScoped<IProjectRepository>(x =>
            x.GetRequiredService<Root>().CreateRedisProjectRepository());
        builder.Services.AddTransient<PlanningFacade>(x =>
            x.GetRequiredService<Root>().CreatePlanningFacade(x.GetRequiredService<IProjectRepository>(),
                x.GetRequiredService<TimeProvider>(), x.GetRequiredService<ResourceAvailabilityRepository>(),
                x.GetRequiredService<SmartScheduleDbContext>(), x.GetRequiredService<IEventsPublisher>(),
                x.GetRequiredService<Root>().CreateUnitOfWork(x.GetRequiredService<SmartScheduleDbContext>())));


// availability
        builder.Services.AddTransient<IAvailabilityFacade, AvailabilityFacade>(x =>
            x.GetRequiredService<Root>().CreateAvailabilityFacade(
                x.GetRequiredService<ResourceAvailabilityRepository>(),
                x.GetRequiredService<SmartScheduleDbContext>(),
                x.GetRequiredService<IEventsPublisher>(),
                x.GetRequiredService<TimeProvider>(),
                x.GetRequiredService<IUnitOfWork>()));
        builder.Services.AddTransient<ResourceAvailabilityRepository>(x =>
            new ResourceAvailabilityRepository(
                x.GetRequiredService<SmartScheduleDbContext>().Database.GetDbConnection()));

// allocation
        builder.Services.AddScoped<IAllocationDbContext>(
            sp => sp.GetRequiredService<SmartScheduleDbContext>());
        builder.Services.AddScoped<IProjectAllocationsRepository, ProjectAllocationsRepository>(
            x => new ProjectAllocationsRepository(x.GetRequiredService<IAllocationDbContext>()));
        builder.Services.AddTransient<AllocationFacade>(
            x => x.GetRequiredService<Root>().CreateAllocationFacade(
                x.GetRequiredService<IEventsPublisher>(),
                x.GetRequiredService<TimeProvider>(),
                x.GetRequiredService<IUnitOfWork>(),
                x.GetRequiredService<IProjectAllocationsRepository>(),
                x.GetRequiredService<ResourceAvailabilityRepository>(),
                x.GetRequiredService<SmartScheduleDbContext>(),
                x.GetRequiredService<AllocatableCapabilityRepository>()));
        builder.Services.AddTransient<PotentialTransfersService>(x => x.GetRequiredService<Root>()
            .CreatePotentialTransfersService(
                x.GetRequiredService<ICashflowRepository>(),
                x.GetRequiredService<IEventsPublisher>(),
                x.GetRequiredService<TimeProvider>(),
                x.GetRequiredService<IUnitOfWork>(),
                x.GetRequiredService<IAllocationDbContext>()));

        builder.Services.AddQuartz(q =>
        {
            var jobKey = new JobKey("PublishMissingDemandsJob");
            q.AddJob<PublishMissingDemandsJob>(opts => opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("PublishMissingDemandsJob-trigger")
                .WithCronSchedule("0 0 * ? * *"));
        });
        // end allocation

        // cashflow
        builder.Services.AddScoped<ICashflowRepository>(
            x => new CashflowRepository(x.GetRequiredService<SmartScheduleDbContext>()));
        builder.Services.AddTransient<CashFlowFacade>(x =>
            x.GetRequiredService<Root>()
                .CreateCashFlowFacade(
                    x.GetRequiredService<ICashflowRepository>(),
                    x.GetRequiredService<IEventsPublisher>(),
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<IUnitOfWork>()));

// employee
        builder.Services.AddScoped<IEmployeeDbContext>(sp => sp.GetRequiredService<SmartScheduleDbContext>());
        builder.Services.AddTransient<EmployeeRepository>(
            x => new EmployeeRepository(x.GetRequiredService<SmartScheduleDbContext>()));
        builder.Services.AddTransient<EmployeeFacade>(
            x => x.GetRequiredService<Root>()
                .CreateEmployeeFacade(
                    x.GetRequiredService<EmployeeRepository>(),
                    x.GetRequiredService<IUnitOfWork>(),
                    x.GetRequiredService<ResourceAvailabilityRepository>(),
                    x.GetRequiredService<SmartScheduleDbContext>(),
                    x.GetRequiredService<IEventsPublisher>(),
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<AllocatableCapabilityRepository>()));

        // device
        builder.Services.AddScoped<IDeviceDbContext>(
            sp => sp.GetRequiredService<SmartScheduleDbContext>());
        builder.Services.AddTransient<DeviceRepository>();
        builder.Services.AddTransient<DeviceFacade>(x =>
            x.GetRequiredService<Root>()
                .CreateDeviceFacade(
                    x.GetRequiredService<DeviceRepository>(),
                    x.GetRequiredService<IUnitOfWork>(),
                    x.GetRequiredService<ResourceAvailabilityRepository>(),
                    x.GetRequiredService<SmartScheduleDbContext>(),
                    x.GetRequiredService<IEventsPublisher>(),
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<AllocatableCapabilityRepository>()));

// resource
        builder.Services.AddTransient<ResourceFacade>(
            x => x.GetRequiredService<Root>()
                .CreateResourceFacade(
                    x.GetRequiredService<EmployeeRepository>(),
                    x.GetRequiredService<IUnitOfWork>(),
                    x.GetRequiredService<DeviceRepository>(),
                    x.GetRequiredService<ResourceAvailabilityRepository>(),
                    x.GetRequiredService<SmartScheduleDbContext>(),
                    x.GetRequiredService<IEventsPublisher>(),
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<AllocatableCapabilityRepository>()));

        //capability planning
        builder.Services.AddScoped<ICapabilitySchedulingDbContext>(
            sp => sp.GetRequiredService<SmartScheduleDbContext>());
        builder.Services.AddTransient<AllocatableCapabilityRepository>();
        builder.Services.AddTransient<ICapabilityFinder, CapabilityFinder>(
            x => x.GetRequiredService<Root>()
                .CreateCapabilityFinder(x.GetRequiredService<ResourceAvailabilityRepository>(),
                    x.GetRequiredService<SmartScheduleDbContext>(),
                    x.GetRequiredService<IEventsPublisher>(),
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<IUnitOfWork>(),
                    x.GetRequiredService<AllocatableCapabilityRepository>()));
        builder.Services.AddTransient<CapabilityScheduler>(x =>
            x.GetRequiredService<Root>().CreateCapabilityScheduler(
                x.GetRequiredService<ResourceAvailabilityRepository>(),
                x.GetRequiredService<SmartScheduleDbContext>(),
                x.GetRequiredService<IEventsPublisher>(),
                x.GetRequiredService<TimeProvider>(),
                x.GetRequiredService<IUnitOfWork>(),
                x.GetRequiredService<AllocatableCapabilityRepository>()));

        //simulation
        builder.Services.AddTransient<SimulationFacade>(x => x.GetRequiredService<Root>().CreateSimulationFacade());

        //risk
        builder.Services.AddScoped<IRiskDbContext>(
            sp => sp.GetRequiredService<SmartScheduleDbContext>());
        builder.Services.AddScoped<RiskPeriodicCheckSagaRepository>();

        builder.Services.AddTransient<RiskPeriodicCheckSagaDispatcher>(x =>
            x.GetRequiredService<Root>()
                .CreateRiskPeriodicCheckSagaDispatcher(
                    x.GetRequiredService<RiskPeriodicCheckSagaRepository>(),
                    x.GetRequiredService<ICashflowRepository>(),
                    x.GetRequiredService<IEventsPublisher>(),
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<IUnitOfWork>(),
                    x.GetRequiredService<IAllocationDbContext>(),
                    x.GetRequiredService<ResourceAvailabilityRepository>(),
                    x.GetRequiredService<SmartScheduleDbContext>(),
                    x.GetRequiredService<AllocatableCapabilityRepository>(),
                    x.GetRequiredService<IRiskPushNotification>()));
        builder.Services.AddTransient<MediatrRiskPeriodicCheckSagaDispatcher>();

        builder.Services.AddTransient<IRiskPushNotification, RiskPushNotification>(x =>
            new RiskPushNotification()); //do not replace this - needed by the tests
        builder.Services.AddTransient<MediatrVerifyCriticalResourceAvailableDuringPlanning>();
        builder.Services.AddTransient<MediatrVerifyEnoughDemandsDuringPlanning>();

        builder.Services.AddTransient<VerifyCriticalResourceAvailableDuringPlanning>(x =>
            x.GetRequiredService<Root>()
                .CreateVerifyCriticalResourceAvailableDuringPlanning(
                    x.GetRequiredService<ResourceAvailabilityRepository>(),
                    x.GetRequiredService<SmartScheduleDbContext>(),
                    x.GetRequiredService<IEventsPublisher>(),
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<IUnitOfWork>(),
                    x.GetRequiredService<IRiskPushNotification>()));
        builder.Services.AddTransient<VerifyEnoughDemandsDuringPlanning>(x =>
            x.GetRequiredService<Root>()
                .CreateVerifyEnoughDemandsDuringPlanning(
                    x.GetRequiredService<IProjectRepository>(),
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<ResourceAvailabilityRepository>(),
                    x.GetRequiredService<SmartScheduleDbContext>(),
                    x.GetRequiredService<IEventsPublisher>(),
                    x.GetRequiredService<IUnitOfWork>(),
                    x.GetRequiredService<EmployeeRepository>(),
                    x.GetRequiredService<DeviceRepository>(),
                    x.GetRequiredService<AllocatableCapabilityRepository>(),
                    x.GetRequiredService<IRiskPushNotification>()));
        builder.Services.AddTransient<VerifyNeededResourcesAvailableInTimeSlot>(x =>
            x.GetRequiredService<Root>()
                .CreateVerifyNeededResourcesAvailableInTimeSlot(
                    x.GetRequiredService<ResourceAvailabilityRepository>(),
                    x.GetRequiredService<SmartScheduleDbContext>(),
                    x.GetRequiredService<IEventsPublisher>(),
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<IUnitOfWork>(),
                    x.GetRequiredService<IRiskPushNotification>()));

        builder.Services.AddQuartz(q =>
        {
            var jobKey1 = new JobKey("RiskPeriodicCheckSagaWeeklyCheckJob");
            q.AddJob<RiskPeriodicCheckSagaWeeklyCheckJob>(opts => opts.WithIdentity(jobKey1));

            q.AddTrigger(opts => opts
                .ForJob(jobKey1)
                .WithIdentity("RiskPeriodicCheckSagaWeeklyCheckJob-trigger")
                .WithCronSchedule("0 0 12 ? * SUN"));
        });

        //quartz
        builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        var app = builder.Build();

        app.Run();
    }
}