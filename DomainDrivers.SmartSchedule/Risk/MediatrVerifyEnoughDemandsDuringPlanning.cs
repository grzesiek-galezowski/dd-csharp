using DomainDrivers.SmartSchedule.Planning;
using MediatR;

namespace DomainDrivers.SmartSchedule.Risk;

public class MediatrVerifyEnoughDemandsDuringPlanning(VerifyEnoughDemandsDuringPlanning verifyEnoughDemandsDuringPlanning)
    : INotificationHandler<CapabilitiesDemanded>
{
    public async Task Handle(CapabilitiesDemanded capabilitiesDemanded, CancellationToken cancellationToken)
    {
        await verifyEnoughDemandsDuringPlanning.Handle(capabilitiesDemanded, cancellationToken);
    }
}