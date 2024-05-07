using DomainDrivers.SmartSchedule.Planning;
using MediatR;

namespace DomainDrivers.SmartSchedule.Risk;

public class MediatrVerifyCriticalResourceAvailableDuringPlanning(
    VerifyCriticalResourceAvailableDuringPlanning verifyCriticalResourceAvailableDuringPlanning)
    : INotificationHandler<CriticalStagePlanned>
{
    public async Task Handle(CriticalStagePlanned criticalStagePlanned, CancellationToken cancellationToken)
    {
        await verifyCriticalResourceAvailableDuringPlanning.Handle(criticalStagePlanned, cancellationToken);
    }
}