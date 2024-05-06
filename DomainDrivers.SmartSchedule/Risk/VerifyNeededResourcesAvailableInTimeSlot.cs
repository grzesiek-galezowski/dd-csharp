using DomainDrivers.SmartSchedule.Availability;
using DomainDrivers.SmartSchedule.Planning;
using DomainDrivers.SmartSchedule.Shared;
using MediatR;

namespace DomainDrivers.SmartSchedule.Risk;

public class VerifyNeededResourcesAvailableInTimeSlot(
    IAvailabilityFacade availabilityFacade,
    IRiskPushNotification riskPushNotification)
    : INotificationHandler<NeededResourcesChosen>
{
    public async Task Handle(NeededResourcesChosen resourcesNeeded, CancellationToken cancellationToken)
    {
        await NotifyAboutNotAvailableResources(resourcesNeeded.NeededResources, resourcesNeeded.TimeSlot,
            resourcesNeeded.ProjectId);
    }

    private async Task NotifyAboutNotAvailableResources(ISet<ResourceId> resourcedIds, TimeSlot timeSlot,
        ProjectId projectId)
    {
        var notAvailable = new HashSet<ResourceId>();
        var calendars = await availabilityFacade.LoadCalendars(resourcedIds, timeSlot);

        foreach (var resourceId in resourcedIds)
        {
            if (calendars.Get(resourceId).AvailableSlots().Any(x => timeSlot.Within(x) == false))
            {
                notAvailable.Add(resourceId);
            }
        }

        if (notAvailable.Any())
        {
            riskPushNotification.NotifyAboutResourcesNotAvailable(projectId, notAvailable);
        }
    }
}