using System.Data;
using System.Text.Json;
using DomainDrivers.SmartSchedule.Allocation;
using DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;
using DomainDrivers.SmartSchedule.Allocation.Cashflow;
using DomainDrivers.SmartSchedule.Availability;
using DomainDrivers.SmartSchedule.Optimization;
using DomainDrivers.SmartSchedule.Planning;
using DomainDrivers.SmartSchedule.Planning.Parallelization;
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
        builder.Services.AddSingleton<IConnectionMultiplexer>(x => x.GetRequiredService<Root>().RedisConnectionMultiplexer);

        ArgumentNullException.ThrowIfNull(postgresConnectionString);
        ArgumentNullException.ThrowIfNull(redisConnectionString);

        var dataSource = new NpgsqlDataSourceBuilder(postgresConnectionString)
            .ConfigureJsonOptions(new JsonSerializerOptions { IgnoreReadOnlyProperties = true, IgnoreReadOnlyFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase})
            .EnableDynamicJson()
            .Build();
        builder.Services.AddDbContext<SmartScheduleDbContext>(options => { options.UseNpgsql(dataSource); });
        builder.Services.AddScoped<IDbConnection>(x => x.GetRequiredService<SmartScheduleDbContext>().Database.GetDbConnection());

//shared
        builder.Services.AddSingleton<Root>(x => 
            new Root(redisConnectionString!, postgresConnectionString!));
//TimeProvider and EventsPublisher must be in container
        builder.Services.AddSingleton<TimeProvider>(x => x.GetRequiredService<Root>().CreateTimeProvider());
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SharedConfiguration).Assembly));
        builder.Services.AddScoped<IEventsPublisher, EventsPublisher>(
            x => x.GetRequiredService<Root>().CreateEventsPublisher(x.GetRequiredService<IMediator>()));
        //unit of work can be moved inside the root later:
        builder.Services.AddTransient<IUnitOfWork, UnitOfWork>(x => 
            x.GetRequiredService<Root>().CreateUnitOfWork(x.GetRequiredService<SmartScheduleDbContext>()));

// planning
        builder.Services.AddScoped<IProjectRepository>(x => 
            x.GetRequiredService<Root>().CreateRedisProjectRepository());
        builder.Services.AddTransient<PlanningFacade>(x =>
            x.GetRequiredService<Root>().CreatePlanningFacade(
                x.GetRequiredService<IProjectRepository>(),
                x.GetRequiredService<TimeProvider>(),
                x.GetRequiredService<Root>().CreateAvailabilityFacade(
                    x.GetRequiredService<ResourceAvailabilityRepository>(), 
                    x.GetRequiredService<SmartScheduleDbContext>(),
                    x.GetRequiredService<IEventsPublisher>(), 
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<IUnitOfWork>()),
                x.GetRequiredService<IMediator>()));


// availability
        builder.Services.AddTransient<IAvailabilityFacade, AvailabilityFacade>(x =>
            x.GetRequiredService<Root>().CreateAvailabilityFacade(
                x.GetRequiredService<ResourceAvailabilityRepository>(), 
                x.GetRequiredService<SmartScheduleDbContext>(),
                x.GetRequiredService<IEventsPublisher>(), 
                x.GetRequiredService<TimeProvider>(),
                x.GetRequiredService<IUnitOfWork>()));
        builder.Services.AddTransient<ResourceAvailabilityRepository>(x =>
            new ResourceAvailabilityRepository(x.GetRequiredService<SmartScheduleDbContext>().Database.GetDbConnection()));

// allocation
        builder.Services.AddScoped<IAllocationDbContext>(
            sp => sp.GetRequiredService<SmartScheduleDbContext>());
        builder.Services.AddScoped<IProjectAllocationsRepository, ProjectAllocationsRepository>(
            x => new ProjectAllocationsRepository(x.GetRequiredService<IAllocationDbContext>()));
        builder.Services.AddTransient<AllocationFacade>(
            x =>
            {
                return x.GetRequiredService<Root>().CreateAllocationFacade(
                    x.GetRequiredService<IEventsPublisher>(),
                    x.GetRequiredService<TimeProvider>(), 
                    x.GetRequiredService<IUnitOfWork>(),
                    x.GetRequiredService<IProjectAllocationsRepository>(),
                    x.GetRequiredService<ResourceAvailabilityRepository>(),
                    x.GetRequiredService<SmartScheduleDbContext>(), 
                    x.GetRequiredService<AllocatableCapabilityRepository>());
            });
        builder.Services.AddTransient<PotentialTransfersService>(x => new PotentialTransfersService(
            x.GetRequiredService<SimulationFacade>(),
            x.GetRequiredService<CashFlowFacade>(),
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
                .CreateRiskPeriodicCheckSagaDispatcher(x.GetRequiredService<RiskPeriodicCheckSagaRepository>(),
                    x.GetRequiredService<ICashflowRepository>(),
                    x.GetRequiredService<IEventsPublisher>(),
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<IUnitOfWork>(),
                    x.GetRequiredService<IAllocationDbContext>(),
                    x.GetRequiredService<ResourceAvailabilityRepository>(),
                    x.GetRequiredService<SmartScheduleDbContext>(),
                    x.GetRequiredService<AllocatableCapabilityRepository>(),
                    x.GetRequiredService<IRiskPushNotification>()));

        builder.Services.AddTransient<IRiskPushNotification, RiskPushNotification>(x => new RiskPushNotification()); //do not replace this - needed by the tests
        builder.Services.AddTransient<VerifyCriticalResourceAvailableDuringPlanning>(x => new VerifyCriticalResourceAvailableDuringPlanning(
            x.GetRequiredService<Root>().CreateAvailabilityFacade(
                x.GetRequiredService<ResourceAvailabilityRepository>(), 
                x.GetRequiredService<SmartScheduleDbContext>(),
                x.GetRequiredService<IEventsPublisher>(), 
                x.GetRequiredService<TimeProvider>(),
                x.GetRequiredService<IUnitOfWork>()),
            x.GetRequiredService<IRiskPushNotification>()));
        builder.Services.AddTransient<VerifyEnoughDemandsDuringPlanning>(x => new VerifyEnoughDemandsDuringPlanning(
            x.GetRequiredService<Root>().CreatePlanningFacade(
                x.GetRequiredService<IProjectRepository>(),
                x.GetRequiredService<TimeProvider>(),
                x.GetRequiredService<Root>().CreateAvailabilityFacade(
                    x.GetRequiredService<ResourceAvailabilityRepository>(), 
                    x.GetRequiredService<SmartScheduleDbContext>(),
                    x.GetRequiredService<IEventsPublisher>(), 
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<IUnitOfWork>()),
                x.GetRequiredService<IMediator>()),
            x.GetRequiredService<Root>().CreateSimulationFacade(),
            x.GetRequiredService<Root>()
                .CreateResourceFacade(
                    x.GetRequiredService<EmployeeRepository>(),
                    x.GetRequiredService<IUnitOfWork>(),
                    x.GetRequiredService<DeviceRepository>(), 
                    x.GetRequiredService<ResourceAvailabilityRepository>(), 
                    x.GetRequiredService<SmartScheduleDbContext>(), 
                    x.GetRequiredService<IEventsPublisher>(), 
                    x.GetRequiredService<TimeProvider>(), 
                    x.GetRequiredService<AllocatableCapabilityRepository>()),
            x.GetRequiredService<IRiskPushNotification>()));
        builder.Services.AddTransient<VerifyNeededResourcesAvailableInTimeSlot>(x => new VerifyNeededResourcesAvailableInTimeSlot(
            x.GetRequiredService<Root>().CreateAvailabilityFacade(
                x.GetRequiredService<ResourceAvailabilityRepository>(), 
                x.GetRequiredService<SmartScheduleDbContext>(),
                x.GetRequiredService<IEventsPublisher>(), 
                x.GetRequiredService<TimeProvider>(),
                x.GetRequiredService<IUnitOfWork>()),
            x.GetRequiredService<IRiskPushNotification>()
            ));
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


public class Root
{
    private readonly string _redisConnectionString;
    private readonly string _postgresConnectionString;

    public Root(string redisConnectionString, string postgresConnectionString)
    {
        _redisConnectionString = redisConnectionString;
        _postgresConnectionString = postgresConnectionString;
        RedisConnectionMultiplexer = ConnectionMultiplexer.Connect(_redisConnectionString);
    }

    public TimeProvider CreateTimeProvider()
    {
        return TimeProvider.System;
    }

    public EventsPublisher CreateEventsPublisher(IMediator mediator)
    {
        return new EventsPublisher(mediator);
    }

    public UnitOfWork CreateUnitOfWork(SmartScheduleDbContext dbContext)
    {
        return new UnitOfWork(dbContext);
    }

    public IConnectionMultiplexer RedisConnectionMultiplexer { get; }

    public RedisProjectRepository CreateRedisProjectRepository()
    {
        return new RedisProjectRepository(RedisConnectionMultiplexer);
    }

    public PlanningFacade CreatePlanningFacade(IProjectRepository projectRepository, TimeProvider timeProvider,
        IAvailabilityFacade availabilityFacade, IMediator mediator)
    {
        var eventsPublisher = CreateEventsPublisher(mediator);
        return new PlanningFacade(
            projectRepository, //must be in container
            new StageParallelization(),
            new PlanChosenResources(
                projectRepository,
                availabilityFacade,
                eventsPublisher,
                timeProvider),
            eventsPublisher,
            timeProvider);
    }

    public AvailabilityFacade CreateAvailabilityFacade(ResourceAvailabilityRepository resourceAvailabilityRepository, SmartScheduleDbContext smartScheduleDbContext, IEventsPublisher eventsPublisher, TimeProvider timeProvider, IUnitOfWork unitOfWork)
    {
        return new AvailabilityFacade(
            resourceAvailabilityRepository,
            new ResourceAvailabilityReadModel(smartScheduleDbContext.Database.GetDbConnection()),
            eventsPublisher, //bug fails the tests if changed
            timeProvider,
            unitOfWork);
    }

    public AllocationFacade CreateAllocationFacade(IEventsPublisher eventsPublisher,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork,
        IProjectAllocationsRepository projectAllocationsRepository,
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext, 
        AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new AllocationFacade(
            projectAllocationsRepository,
            CreateAvailabilityFacade(
                resourceAvailabilityRepository,
                smartScheduleDbContext,
                eventsPublisher,
                timeProvider,
                unitOfWork),
            CreateCapabilityFinder(resourceAvailabilityRepository, 
                    smartScheduleDbContext, 
                    eventsPublisher, 
                    timeProvider, 
                    unitOfWork, 
                    allocatableCapabilityRepository),
            eventsPublisher,
            timeProvider,
            unitOfWork);
    }

    public CashFlowFacade CreateCashFlowFacade(
        ICashflowRepository cashflowRepository,
        IEventsPublisher eventsPublisher,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork)
    {
        return new CashFlowFacade(
            cashflowRepository, 
            eventsPublisher,
            timeProvider, 
            unitOfWork);
    }

    public EmployeeFacade CreateEmployeeFacade(EmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher getRequiredService,
        TimeProvider timeProvider,
        AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new EmployeeFacade(
            employeeRepository,
            new ScheduleEmployeeCapabilities(
                employeeRepository, 
                CreateCapabilityScheduler(
                    resourceAvailabilityRepository,
                    smartScheduleDbContext,
                    getRequiredService,
                    timeProvider,
                    unitOfWork, 
                    allocatableCapabilityRepository)),
            unitOfWork
        );
    }

    public DeviceFacade CreateDeviceFacade(
        DeviceRepository deviceRepository,
        IUnitOfWork unitOfWork,
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
        TimeProvider timeProvider,
        AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new DeviceFacade(
            deviceRepository,
            new ScheduleDeviceCapabilities(
                deviceRepository,
                CreateCapabilityScheduler(
                    resourceAvailabilityRepository,
                    smartScheduleDbContext,
                    eventsPublisher,
                    timeProvider,
                    unitOfWork, 
                    allocatableCapabilityRepository)),
            unitOfWork
        );
    }

    public ResourceFacade CreateResourceFacade(EmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        DeviceRepository deviceRepository,
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
        TimeProvider timeProvider,
        AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new ResourceFacade(
            CreateEmployeeFacade(employeeRepository,
                unitOfWork,
                resourceAvailabilityRepository, 
                smartScheduleDbContext, 
                eventsPublisher, 
                timeProvider, 
                allocatableCapabilityRepository),
            CreateDeviceFacade(
                deviceRepository,
                unitOfWork,
                resourceAvailabilityRepository, 
                smartScheduleDbContext, 
                eventsPublisher, 
                timeProvider, 
                allocatableCapabilityRepository));
    }

    public CapabilityScheduler CreateCapabilityScheduler(ResourceAvailabilityRepository resourceAvailabilityRepository, SmartScheduleDbContext smartScheduleDbContext, IEventsPublisher getRequiredService, TimeProvider requiredService, IUnitOfWork unitOfWork, AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new CapabilityScheduler(
            CreateAvailabilityFacade(
                resourceAvailabilityRepository, 
                smartScheduleDbContext,
                getRequiredService, 
                requiredService,
                unitOfWork),
            allocatableCapabilityRepository,
            unitOfWork);
    }

    public CapabilityFinder CreateCapabilityFinder(
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher getRequiredService,
        TimeProvider requiredService,
        IUnitOfWork unitOfWork,
        AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new CapabilityFinder(
            CreateAvailabilityFacade(
                resourceAvailabilityRepository, 
                smartScheduleDbContext,
                getRequiredService, 
                requiredService,
                unitOfWork),
            allocatableCapabilityRepository);
    }

    public OptimizationFacade CreateOptimizationFacade()
    {
        return new OptimizationFacade();
    }

    public SimulationFacade CreateSimulationFacade()
    {
        return new SimulationFacade(CreateOptimizationFacade());
    }

    public RiskPeriodicCheckSagaDispatcher CreateRiskPeriodicCheckSagaDispatcher(
        RiskPeriodicCheckSagaRepository riskPeriodicCheckSagaRepository,
        ICashflowRepository cashflowRepository,
        IEventsPublisher eventsPublisher,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork,
        IAllocationDbContext allocationDbContext,
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        AllocatableCapabilityRepository allocatableCapabilityRepository,
        IRiskPushNotification riskPushNotification)
    {
        return new RiskPeriodicCheckSagaDispatcher(
            riskPeriodicCheckSagaRepository,
            new PotentialTransfersService(
                CreateSimulationFacade(),
                CreateCashFlowFacade(
                    cashflowRepository,
                    eventsPublisher,
                    timeProvider,
                    unitOfWork),
                allocationDbContext),
            CreateCapabilityFinder(
                resourceAvailabilityRepository,
                smartScheduleDbContext,
                eventsPublisher,
                timeProvider,
                unitOfWork,
                allocatableCapabilityRepository),
            riskPushNotification,
            timeProvider,
            unitOfWork);
    }
}

public static class TestConfiguration
{
    private static readonly AsyncLocal<Action<IConfigurationBuilder>> Current = new()
    {
        Value = c => { }
    };

    internal static void AddTestConfiguration(this IConfigurationBuilder configurationBuilder)
    {
        Current.Value!(configurationBuilder);
    }

    public static void Set(Action<IConfigurationBuilder> action)
    {
        Current.Value = action;
    }
}
