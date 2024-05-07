using DomainDrivers.SmartSchedule.Allocation;
using DomainDrivers.SmartSchedule.Allocation.Cashflow;
using DomainDrivers.SmartSchedule.Availability;
using MediatR;

namespace DomainDrivers.SmartSchedule.Risk;

public class MediatrRiskPeriodicCheckSagaDispatcher(RiskPeriodicCheckSagaDispatcher riskPeriodicCheckSagaDispatcher)
    :
        INotificationHandler<EarningsRecalculated>, INotificationHandler<ProjectAllocationScheduled>,
        INotificationHandler<ResourceTakenOver>, INotificationHandler<NotSatisfiedDemands>
{
    //remember about transactions spanning saga and potential external system
    public async Task Handle(ProjectAllocationScheduled @event, CancellationToken cancellationToken)
    {
        await riskPeriodicCheckSagaDispatcher.Handle(@event, cancellationToken);
    }
    
    //remember about transactions spanning saga and potential external system
    public async Task Handle(NotSatisfiedDemands @event, CancellationToken cancellationToken)
    {
        await riskPeriodicCheckSagaDispatcher.Handle(@event, cancellationToken);
    }
    
    //remember about transactions spanning saga and potential external system
    public async Task Handle(EarningsRecalculated @event, CancellationToken cancellationToken)
    {
        await riskPeriodicCheckSagaDispatcher.Handle(@event, cancellationToken);
    }
    
    //remember about transactions spanning saga and potential external system
    public async Task Handle(ResourceTakenOver @event, CancellationToken cancellationToken)
    {
        await riskPeriodicCheckSagaDispatcher.Handle(@event, cancellationToken);
    }

    public async Task HandleWeeklyCheck()
    {
        await riskPeriodicCheckSagaDispatcher.HandleWeeklyCheck();
    }
}