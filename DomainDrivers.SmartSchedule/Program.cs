using System.Data;
using System.Text.Json;
using DomainDrivers.SmartSchedule;
using DomainDrivers.SmartSchedule.Allocation;
using DomainDrivers.SmartSchedule.Availability;
using DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;
using DomainDrivers.SmartSchedule.Allocation.Cashflow;
using DomainDrivers.SmartSchedule.Optimization;
using DomainDrivers.SmartSchedule.Planning;
using DomainDrivers.SmartSchedule.Planning.Parallelization;
using DomainDrivers.SmartSchedule.Resource;
using DomainDrivers.SmartSchedule.Resource.Device;
using DomainDrivers.SmartSchedule.Resource.Employee;
using DomainDrivers.SmartSchedule.Risk;
using DomainDrivers.SmartSchedule.Shared;
using DomainDrivers.SmartSchedule.Simulation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Quartz;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString!));

var dataSource = new NpgsqlDataSourceBuilder(postgresConnectionString)
    .ConfigureJsonOptions(new JsonSerializerOptions { IgnoreReadOnlyProperties = true, IgnoreReadOnlyFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase})
    .EnableDynamicJson()
    .Build();
builder.Services.AddDbContext<SmartScheduleDbContext>(options => { options.UseNpgsql(dataSource); });
builder.Services.AddScoped<IDbConnection>(sp => sp.GetRequiredService<SmartScheduleDbContext>().Database.GetDbConnection());
builder.Services.AddShared();

// planning
builder.Services.AddScoped<IProjectRepository>(x => 
    new RedisProjectRepository(x.GetRequiredService<IConnectionMultiplexer>()));
builder.Services.AddTransient<PlanningFacade>(x =>
{
    var requiredService = x.GetRequiredService<IEventsPublisher>();
    var timeProvider = x.GetRequiredService<TimeProvider>();

    return new PlanningFacade(
        x.GetRequiredService<IProjectRepository>(), //must be in container
        new StageParallelization(),
        new PlanChosenResources(
            x.GetRequiredService<IProjectRepository>(),
            x.GetRequiredService<IAvailabilityFacade>(),
            requiredService,
            timeProvider),
        requiredService,
        timeProvider);
});


// availability
builder.Services.AddTransient<IAvailabilityFacade, AvailabilityFacade>(x => new AvailabilityFacade(
    x.GetRequiredService<ResourceAvailabilityRepository>(),
    new ResourceAvailabilityReadModel(x.GetRequiredService<IDbConnection>()), //x.GetRequiredService<ResourceAvailabilityReadModel>()
    x.GetRequiredService<IEventsPublisher>(),
    x.GetRequiredService<TimeProvider>(),
    x.GetRequiredService<IUnitOfWork>()));
builder.Services.AddTransient<ResourceAvailabilityRepository>(x =>
    new ResourceAvailabilityRepository(x.GetRequiredService<IDbConnection>()));

// allocation
builder.Services.AddScoped<IAllocationDbContext>(
    sp => sp.GetRequiredService<SmartScheduleDbContext>());
builder.Services.AddScoped<IProjectAllocationsRepository, ProjectAllocationsRepository>(
    x => new ProjectAllocationsRepository(x.GetRequiredService<IAllocationDbContext>()));
builder.Services.AddTransient<AllocationFacade>(
    x => new AllocationFacade(
        x.GetRequiredService<IProjectAllocationsRepository>(),
        x.GetRequiredService<IAvailabilityFacade>(),
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
IServiceCollection temp = builder.Services;


builder.Services.AddCashFlow();
builder.Services.AddEmployee();
builder.Services.AddDevice();
builder.Services.AddResource();
builder.Services.AddCapabilityPlanning();
builder.Services.AddOptimization();
builder.Services.AddSimulation();
builder.Services.AddRisk();
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

app.Run();

public partial class Program;