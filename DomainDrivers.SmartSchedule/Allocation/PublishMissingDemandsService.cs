using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Allocation;

public class PublishMissingDemandsService(
    ProjectAllocationsRepository projectAllocationsRepository,
    CreateHourlyDemandsSummaryService createHourlyDemandsSummaryService,
    IEventsPublisher eventsPublisher,
    TimeProvider timeProvider)
{
    public async Task Publish()
    {
        var when = timeProvider.GetUtcNow().DateTime;
        var projectAllocations =
           await projectAllocationsRepository.FindAllContainingDate(when);
        var missingDemands = createHourlyDemandsSummaryService.Create(projectAllocations, when);
        //add metadata to event
        //if needed call EventStore and translate multiple private events to a new published event
        await eventsPublisher.Publish(missingDemands);
    }
}

public class CreateHourlyDemandsSummaryService
{
    public NotSatisfiedDemands Create(IList<ProjectAllocations> projectAllocations, DateTime when)
    {
        var missingDemands =
            projectAllocations
                .Where(x => x.HasTimeSlot)
                .ToDictionary(x => x.ProjectId, x => x.MissingDemands());
        return new NotSatisfiedDemands(missingDemands, when);
    }
}