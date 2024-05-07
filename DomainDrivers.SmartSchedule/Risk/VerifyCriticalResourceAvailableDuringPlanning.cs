using DomainDrivers.SmartSchedule.Availability;
using DomainDrivers.SmartSchedule.Planning;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Risk;

public class VerifyCriticalResourceAvailableDuringPlanning(
    IAvailabilityFacade availabilityFacade,
    IRiskPushNotification riskPushNotification)
{
    public async Task Handle(CriticalStagePlanned criticalStagePlanned, CancellationToken cancellationToken)
    {
        if (criticalStagePlanned.CriticalResource == null)
        {
            return;
        }

        var calendar =
            await availabilityFacade.LoadCalendar(criticalStagePlanned.CriticalResource,
                criticalStagePlanned.StageTimeSlot);

        if (!ResourceIsAvailable(criticalStagePlanned.StageTimeSlot, calendar))
        {
            riskPushNotification.NotifyAboutCriticalResourceNotAvailable(criticalStagePlanned.ProjectId,
                criticalStagePlanned.CriticalResource, criticalStagePlanned.StageTimeSlot);
        }
    }

    private bool ResourceIsAvailable(TimeSlot timeSlot, Calendar calendar)
    {
        return calendar.AvailableSlots().Any(slot => slot == timeSlot);
    }
}