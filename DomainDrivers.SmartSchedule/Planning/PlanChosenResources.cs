using DomainDrivers.SmartSchedule.Availability;
using DomainDrivers.SmartSchedule.Planning.Parallelization;
using DomainDrivers.SmartSchedule.Planning.Scheduling;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Planning;

public class PlanChosenResources(
    IProjectRepository projectRepository,
    IAvailabilityFacade availabilityFacade,
    IEventsPublisher eventsPublisher,
    TimeProvider timeProvider)
{
    public async Task DefineResourcesWithinDates(ProjectId projectId, ISet<ResourceId> chosenResources,
        TimeSlot timeBoundaries)
    {
        var project = await projectRepository.GetById(projectId);
        project.AddChosenResources(new ChosenResources(chosenResources, timeBoundaries));
        await projectRepository.Save(project);
        await eventsPublisher.Publish(new NeededResourcesChosen(projectId, chosenResources, timeBoundaries,
            timeProvider.GetUtcNow().DateTime));
    }

    public async Task AdjustStagesToResourceAvailability(ProjectId projectId, TimeSlot timeBoundaries,
        params Stage[] stages)
    {
        var neededResources = NeededResources(stages);
        var project = await projectRepository.GetById(projectId);
        await DefineResourcesWithinDates(projectId, neededResources, timeBoundaries);
        var neededResourcesCalendars = await availabilityFacade.LoadCalendars(neededResources, timeBoundaries);
        var schedule = CreateScheduleAdjustingToCalendars(neededResourcesCalendars, stages.ToList());
        project.AddSchedule(schedule);
        await projectRepository.Save(project);
    }

    private Schedule CreateScheduleAdjustingToCalendars(Calendars neededResourcesCalendars,
        IList<Stage> stages)
    {
        return Schedule.BasedOnChosenResourcesAvailability(neededResourcesCalendars, stages);
    }

    private ISet<ResourceId> NeededResources(Stage[] stages)
    {
        return stages.SelectMany(stage => stage.Resources).ToHashSet();
    }
}