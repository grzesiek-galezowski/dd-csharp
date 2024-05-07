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

    public AvailabilityFacade CreateAvailabilityFacade(
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
        TimeProvider timeProvider)
    {
        return new AvailabilityFacade(
            resourceAvailabilityRepository,
            new ResourceAvailabilityReadModel(smartScheduleDbContext.Database.GetDbConnection()),
            eventsPublisher, //bug fails the tests if changed
            timeProvider,
            CreateUnitOfWork(smartScheduleDbContext));
    }

    public AllocationFacade CreateAllocationFacade(IEventsPublisher eventsPublisher,
        TimeProvider timeProvider,
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
                timeProvider),
            CreateCapabilityFinder(resourceAvailabilityRepository, 
                smartScheduleDbContext, 
                eventsPublisher, 
                timeProvider, 
                allocatableCapabilityRepository),
            eventsPublisher,
            timeProvider,
            CreateUnitOfWork(smartScheduleDbContext));
    }

    public CashFlowFacade CreateCashFlowFacade(
        ICashflowRepository cashflowRepository,
        IEventsPublisher eventsPublisher,
        TimeProvider timeProvider, 
        SmartScheduleDbContext smartScheduleDbContext)
    {
        return new CashFlowFacade(
            cashflowRepository, 
            eventsPublisher,
            timeProvider, 
            CreateUnitOfWork(smartScheduleDbContext));
    }

    public EmployeeFacade CreateEmployeeFacade(EmployeeRepository employeeRepository,
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
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
                    eventsPublisher,
                    timeProvider, 
                    allocatableCapabilityRepository)),
            CreateUnitOfWork(smartScheduleDbContext)
        );
    }

    public DeviceFacade CreateDeviceFacade(
        DeviceRepository deviceRepository,
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
                    allocatableCapabilityRepository)),
            CreateUnitOfWork(smartScheduleDbContext)
        );
    }

    public ResourceFacade CreateResourceFacade(EmployeeRepository employeeRepository,
        DeviceRepository deviceRepository,
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
        TimeProvider timeProvider,
        AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new ResourceFacade(
            CreateEmployeeFacade(employeeRepository,
                resourceAvailabilityRepository, 
                smartScheduleDbContext, 
                eventsPublisher, 
                timeProvider, 
                allocatableCapabilityRepository),
            CreateDeviceFacade(
                deviceRepository,
                resourceAvailabilityRepository, 
                smartScheduleDbContext, 
                eventsPublisher, 
                timeProvider, 
                allocatableCapabilityRepository));
    }

    public CapabilityScheduler CreateCapabilityScheduler(
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher getRequiredService,
        TimeProvider requiredService,
        AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new CapabilityScheduler(
            CreateAvailabilityFacade(
                resourceAvailabilityRepository, 
                smartScheduleDbContext,
                getRequiredService, 
                requiredService),
            allocatableCapabilityRepository,
            CreateUnitOfWork(smartScheduleDbContext));
    }

    public CapabilityFinder CreateCapabilityFinder(
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher getRequiredService,
        TimeProvider requiredService,
        AllocatableCapabilityRepository allocatableCapabilityRepository)
    {
        return new CapabilityFinder(
            CreateAvailabilityFacade(
                resourceAvailabilityRepository, 
                smartScheduleDbContext,
                getRequiredService, 
                requiredService),
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
        IAllocationDbContext allocationDbContext,
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        AllocatableCapabilityRepository allocatableCapabilityRepository,
        IRiskPushNotification riskPushNotification)
    {
        return new RiskPeriodicCheckSagaDispatcher(
            riskPeriodicCheckSagaRepository, 
            CreatePotentialTransfersService(
                cashflowRepository, 
                eventsPublisher, 
                timeProvider, 
                allocationDbContext, 
                smartScheduleDbContext), 
            CreateCapabilityFinder(
                resourceAvailabilityRepository,
                smartScheduleDbContext,
                eventsPublisher,
                timeProvider,
                allocatableCapabilityRepository), 
            riskPushNotification, 
            timeProvider, 
            CreateUnitOfWork(smartScheduleDbContext));
    }

    public PotentialTransfersService CreatePotentialTransfersService(
        ICashflowRepository cashflowRepository,
        IEventsPublisher eventsPublisher,
        TimeProvider timeProvider,
        IAllocationDbContext allocationDbContext,
        SmartScheduleDbContext smartScheduleDbContext)
    {
        return new PotentialTransfersService(
            CreateSimulationFacade(),
            CreateCashFlowFacade(
                cashflowRepository,
                eventsPublisher,
                timeProvider, 
                smartScheduleDbContext),
            allocationDbContext);
    }

    public VerifyNeededResourcesAvailableInTimeSlot CreateVerifyNeededResourcesAvailableInTimeSlot(
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
        TimeProvider timeProvider,
        IRiskPushNotification riskPushNotification)
    {
        return new VerifyNeededResourcesAvailableInTimeSlot(
            CreateAvailabilityFacade(
                resourceAvailabilityRepository, 
                smartScheduleDbContext,
                eventsPublisher, 
                timeProvider),
            riskPushNotification
        );
    }

    public VerifyEnoughDemandsDuringPlanning CreateVerifyEnoughDemandsDuringPlanning(
        IProjectRepository projectRepository,
        TimeProvider timeProvider,
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
        EmployeeRepository employeeRepository,
        DeviceRepository deviceRepository,
        AllocatableCapabilityRepository allocatableCapabilityRepository,
        IRiskPushNotification riskPushNotification)
    {
        return new VerifyEnoughDemandsDuringPlanning(
                CreatePlanningFacade(projectRepository,
                    timeProvider,
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
                    timeProvider, 
                    allocatableCapabilityRepository),
                riskPushNotification);
    }

    public PlanningFacade CreatePlanningFacade(
        IProjectRepository projectRepository,
        TimeProvider timeProvider,
        ResourceAvailabilityRepository resourceAvailabilityRepository,
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
                    eventsPublisher, 
                    timeProvider),
                eventsPublisher,
                timeProvider),
            eventsPublisher,
            timeProvider);
    }

    public VerifyCriticalResourceAvailableDuringPlanning CreateVerifyCriticalResourceAvailableDuringPlanning(
        ResourceAvailabilityRepository resourceAvailabilityRepository,
        SmartScheduleDbContext smartScheduleDbContext,
        IEventsPublisher eventsPublisher,
        TimeProvider timeProvider,
        IRiskPushNotification riskPushNotification)
    {
        return new VerifyCriticalResourceAvailableDuringPlanning(
            CreateAvailabilityFacade(
                resourceAvailabilityRepository,
                smartScheduleDbContext,
                eventsPublisher,
                timeProvider),
            riskPushNotification);
    }
}