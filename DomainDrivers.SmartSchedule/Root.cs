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
using StackExchange.Redis;

namespace DomainDrivers.SmartSchedule;

public class AppLogicRoot
{
    public TimeProvider _timeProvider;

    public AppLogicRoot(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public AvailabilityFacade CreateAvailabilityFacade(
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        IEventsPublisher eventsPublisher,
        IResourceAvailabilityReadModel resourceAvailabilityReadModel,
        IUnitOfWork unitOfWork)
    {
        return new AvailabilityFacade(
            resourceAvailabilityRepository,
            resourceAvailabilityReadModel,
            eventsPublisher, //bug fails the tests if changed
            _timeProvider,
            unitOfWork);
    }

    public CapabilityFinder CreateCapabilityFinder(
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        IEventsPublisher getRequiredService,
        IAllocatableCapabilityRepository allocatableCapabilityRepository,
        IResourceAvailabilityReadModel resourceAvailabilityReadModel,
        IUnitOfWork unitOfWork)
    {
        return new CapabilityFinder(
            CreateAvailabilityFacade(
                resourceAvailabilityRepository,
                getRequiredService,
                resourceAvailabilityReadModel,
                unitOfWork),
            allocatableCapabilityRepository);
    }
}

public class Root
{
    private readonly string _redisConnectionString;
    private readonly string _postgresConnectionString;
    private readonly TimeProvider _timeProvider;
    private readonly AppLogicRoot _appLogicRoot;

    public Root(string redisConnectionString, string postgresConnectionString, TimeProvider timeProvider)
    {
        _redisConnectionString = redisConnectionString;
        _postgresConnectionString = postgresConnectionString;
        _timeProvider = timeProvider;
        RedisConnectionMultiplexer = ConnectionMultiplexer.Connect(_redisConnectionString);
        _appLogicRoot = new AppLogicRoot(_timeProvider);
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

    public AvailabilityFacade CreateAvailabilityFacade(
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher)
    {
        return _appLogicRoot.CreateAvailabilityFacade(
            resourceAvailabilityRepository,
            eventsPublisher,
            CreateResourceAvailabilityReadModel(smartScheduleDbContext),
            CreateUnitOfWork(smartScheduleDbContext));
    }

    private static ResourceAvailabilityReadModel CreateResourceAvailabilityReadModel(SmartScheduleDbContext smartScheduleDbContext)
    {
        return new ResourceAvailabilityReadModel(smartScheduleDbContext.Database.GetDbConnection());
    }

    public AllocationFacade CreateAllocationFacade(
        IEventsPublisher eventsPublisher,
        IProjectAllocationsRepository projectAllocationsRepository,
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext, 
        AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new AllocationFacade(
            projectAllocationsRepository,
            _appLogicRoot.CreateAvailabilityFacade(
                resourceAvailabilityRepository,
                eventsPublisher,
                CreateResourceAvailabilityReadModel(smartScheduleDbContext),
                CreateUnitOfWork(smartScheduleDbContext)),
            CreateCapabilityFinder(resourceAvailabilityRepository, 
                smartScheduleDbContext, 
                eventsPublisher, 
                allocatableCapabilityRepository),
            eventsPublisher,
            _timeProvider,
            CreateUnitOfWork(smartScheduleDbContext));
    }

    public CashFlowFacade CreateCashFlowFacade(
        ICashflowRepository cashflowRepository,
        IEventsPublisher eventsPublisher, 
        SmartScheduleDbContext smartScheduleDbContext)
    {
        return new CashFlowFacade(
            cashflowRepository, 
            eventsPublisher,
            _timeProvider, 
            CreateUnitOfWork(smartScheduleDbContext));
    }

    public EmployeeFacade CreateEmployeeFacade(EmployeeRepository employeeRepository,
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
        AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new EmployeeFacade(
            employeeRepository,
            new ScheduleEmployeeCapabilities(
                employeeRepository, 
                CreateCapabilityScheduler(
                    resourceAvailabilityRepository,
                    smartScheduleDbContext,
                    eventsPublisher, 
                    allocatableCapabilityRepository)),
            CreateUnitOfWork(smartScheduleDbContext)
        );
    }

    public DeviceFacade CreateDeviceFacade(
        DeviceRepository deviceRepository,
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
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
                    allocatableCapabilityRepository)),
            CreateUnitOfWork(smartScheduleDbContext)
        );
    }

    public ResourceFacade CreateResourceFacade(EmployeeRepository employeeRepository,
        DeviceRepository deviceRepository,
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
        AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new ResourceFacade(
            CreateEmployeeFacade(employeeRepository,
                resourceAvailabilityRepository, 
                smartScheduleDbContext, 
                eventsPublisher, 
                allocatableCapabilityRepository),
            CreateDeviceFacade(
                deviceRepository,
                resourceAvailabilityRepository, 
                smartScheduleDbContext, 
                eventsPublisher, 
                allocatableCapabilityRepository));
    }

    public CapabilityScheduler CreateCapabilityScheduler(
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher getRequiredService,
        AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new CapabilityScheduler(
            CreateAvailabilityFacade(
                resourceAvailabilityRepository, 
                smartScheduleDbContext,
                getRequiredService),
            allocatableCapabilityRepository,
            CreateUnitOfWork(smartScheduleDbContext));
    }

    public CapabilityFinder CreateCapabilityFinder(
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher getRequiredService,
        IAllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return _appLogicRoot.CreateCapabilityFinder(
            resourceAvailabilityRepository,
            getRequiredService,
            allocatableCapabilityRepository,
            CreateResourceAvailabilityReadModel(smartScheduleDbContext),
            CreateUnitOfWork(smartScheduleDbContext));
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
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        AllocatableCapabilityRepository allocatableCapabilityRepository,
        IRiskPushNotification riskPushNotification)
    {
        return new RiskPeriodicCheckSagaDispatcher(
            riskPeriodicCheckSagaRepository,
            CreatePotentialTransfersService(
                cashflowRepository,
                eventsPublisher, 
                smartScheduleDbContext),
            CreateCapabilityFinder(
                resourceAvailabilityRepository,
                smartScheduleDbContext,
                eventsPublisher,
                allocatableCapabilityRepository),
            riskPushNotification,
            _timeProvider,
            CreateUnitOfWork(smartScheduleDbContext));
    }

    public PotentialTransfersService CreatePotentialTransfersService(
        ICashflowRepository cashflowRepository,
        IEventsPublisher eventsPublisher,
        SmartScheduleDbContext smartScheduleDbContext)
    {
        return new PotentialTransfersService(
            CreateSimulationFacade(),
            CreateCashFlowFacade(
                cashflowRepository,
                eventsPublisher, 
                smartScheduleDbContext),
            smartScheduleDbContext);
    }

    public VerifyNeededResourcesAvailableInTimeSlot CreateVerifyNeededResourcesAvailableInTimeSlot(
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
        IRiskPushNotification riskPushNotification)
    {
        return new VerifyNeededResourcesAvailableInTimeSlot(
            CreateAvailabilityFacade(
                resourceAvailabilityRepository, 
                smartScheduleDbContext,
                eventsPublisher),
            riskPushNotification
        );
    }

    public VerifyEnoughDemandsDuringPlanning CreateVerifyEnoughDemandsDuringPlanning(
        IProjectRepository projectRepository,
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
        EmployeeRepository employeeRepository,
        DeviceRepository deviceRepository,
        AllocatableCapabilityRepository allocatableCapabilityRepository,
        IRiskPushNotification riskPushNotification)
    {
        return new VerifyEnoughDemandsDuringPlanning(
                CreatePlanningFacade(projectRepository,
                    resourceAvailabilityRepository,
                    smartScheduleDbContext,
                    eventsPublisher),
                CreateSimulationFacade(),
                CreateResourceFacade(
                    employeeRepository,
                    deviceRepository, 
                    resourceAvailabilityRepository, 
                    smartScheduleDbContext, 
                    eventsPublisher, 
                    allocatableCapabilityRepository),
                riskPushNotification);
    }

    public PlanningFacade CreatePlanningFacade(
        IProjectRepository projectRepository,
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher)
    {
        return new PlanningFacade(
            projectRepository, //must be in container
            new StageParallelization(),
            new PlanChosenResources(
                projectRepository,
                CreateAvailabilityFacade(
                    resourceAvailabilityRepository, 
                    smartScheduleDbContext,
                    eventsPublisher),
                eventsPublisher,
                _timeProvider),
            eventsPublisher,
            _timeProvider);
    }

    public VerifyCriticalResourceAvailableDuringPlanning CreateVerifyCriticalResourceAvailableDuringPlanning(
        IResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
        IRiskPushNotification riskPushNotification)
    {
        return new VerifyCriticalResourceAvailableDuringPlanning(
            CreateAvailabilityFacade(
                resourceAvailabilityRepository,
                smartScheduleDbContext,
                eventsPublisher),
            riskPushNotification);
    }
}