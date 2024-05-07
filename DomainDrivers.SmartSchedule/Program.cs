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
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>(x => 
            x.GetRequiredService<Root>().CreateUnitOfWork(x.GetRequiredService<SmartScheduleDbContext>()));

// planning
        builder.Services.AddScoped<IProjectRepository>(x => 
            x.GetRequiredService<Root>().CreateRedisProjectRepository());
        builder.Services.AddTransient<PlanningFacade>(x =>
        {
            var timeProvider = x.GetRequiredService<TimeProvider>();

            return x.GetRequiredService<Root>().CreatePlanningFacade(
                x.GetRequiredService<IProjectRepository>(), 
                timeProvider, 
                x.GetRequiredService<IAvailabilityFacade>(), 
                x.GetRequiredService<IMediator>());
        });


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
            x => new AllocationFacade(
                x.GetRequiredService<IProjectAllocationsRepository>(),
                x.GetRequiredService<Root>().CreateAvailabilityFacade(
                    x.GetRequiredService<ResourceAvailabilityRepository>(), 
                    x.GetRequiredService<SmartScheduleDbContext>(),
                    x.GetRequiredService<IEventsPublisher>(), 
                    x.GetRequiredService<TimeProvider>(),
                    x.GetRequiredService<IUnitOfWork>()),
                x.GetRequiredService<ICapabilityFinder>(),
                x.GetRequiredService<IEventsPublisher>(),
                x.GetRequiredService<TimeProvider>(),
                x.GetRequiredService<IUnitOfWork>()));
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
            new CashFlowFacade(
                x.GetRequiredService<ICashflowRepository>(), 
                x.GetRequiredService<IEventsPublisher>(),
                x.GetRequiredService<TimeProvider>(), 
                x.GetRequiredService<IUnitOfWork>()));

// employee
        builder.Services.AddScoped<IEmployeeDbContext>(
            sp => sp.GetRequiredService<SmartScheduleDbContext>());
        builder.Services.AddTransient<EmployeeRepository>(
            x => new EmployeeRepository(x.GetRequiredService<SmartScheduleDbContext>()));
        builder.Services.AddTransient<EmployeeFacade>(
            x => new EmployeeFacade(
                x.GetRequiredService<EmployeeRepository>(),
                new ScheduleEmployeeCapabilities(
                    x.GetRequiredService<EmployeeRepository>(), 
                    x.GetRequiredService<CapabilityScheduler>()),
                x.GetRequiredService<IUnitOfWork>()
            ));

// device
        builder.Services.AddDevice();

// resource
        builder.Services.AddResource();

        builder.Services.AddCapabilityPlanning();
        builder.Services.AddOptimization();
        builder.Services.AddSimulation();
        builder.Services.AddRisk();
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
