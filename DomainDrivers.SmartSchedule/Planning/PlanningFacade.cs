using DomainDrivers.SmartSchedule.Availability;
using DomainDrivers.SmartSchedule.Planning.Parallelization;
using DomainDrivers.SmartSchedule.Planning.Scheduling;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Planning;

public class PlanningFacade(
    IProjectRepository projectRepository,
    StageParallelization parallelization,
    PlanChosenResources resourcesPlanning,
    IEventsPublisher eventsPublisher,
    TimeProvider timeProvider)
{
    public async Task<ProjectId> AddNewProject(string name, params Stage[] stages)
    {
        var parallelizedStages = parallelization.Of(new HashSet<Stage>(stages));
        return await AddNewProject(name, parallelizedStages);
    }

    public async Task<ProjectId> AddNewProject(string name, ParallelStagesList parallelizedStages)
    {
        var project = new Project(name, parallelizedStages);
        await projectRepository.Save(project);
        return project.Id;
    }

    public async Task DefineStartDate(ProjectId projectId, DateTime possibleStartDate)
    {
        var project = await projectRepository.GetById(projectId);
        project.AddSchedule(possibleStartDate);
        await projectRepository.Save(project);
    }

    public async Task DefineProjectStages(ProjectId projectId, params Stage[] stages)
    {
        var project = await projectRepository.GetById(projectId);
        var parallelizedStages = parallelization.Of(new HashSet<Stage>(stages));
        project.DefineStages(parallelizedStages);
        await projectRepository.Save(project);
    }

    public async Task AddDemands(ProjectId projectId, Demands demands)
    {
        var project = await projectRepository.GetById(projectId);
        project.AddDemands(demands);
        await projectRepository.Save(project);
        await eventsPublisher.Publish(new CapabilitiesDemanded(projectId, project.AllDemands,
            timeProvider.GetUtcNow().DateTime));
    }

    public async Task DefineDemandsPerStage(ProjectId projectId, DemandsPerStage demandsPerStage)
    {
        var project = await projectRepository.GetById(projectId);
        project.AddDemandsPerStage(demandsPerStage);
        await projectRepository.Save(project);
        await eventsPublisher.Publish(new CapabilitiesDemanded(projectId, project.AllDemands,
            timeProvider.GetUtcNow().DateTime));
    }

    public async Task DefineResourcesWithinDates(ProjectId projectId, HashSet<ResourceId> chosenResources,
        TimeSlot timeBoundaries)
    {
        await resourcesPlanning.DefineResourcesWithinDates(projectId, chosenResources, timeBoundaries);
    }

    public async Task AdjustStagesToResourceAvailability(ProjectId projectId, TimeSlot timeBoundaries,
        params Stage[] stages)
    {
        await resourcesPlanning.AdjustStagesToResourceAvailability(projectId, timeBoundaries, stages);
    }

    public async Task PlanCriticalStageWithResource(ProjectId projectId, Stage criticalStage,
        ResourceId resourceId,
        TimeSlot stageTimeSlot)
    {
        var project = await projectRepository.GetById(projectId);
        project.AddSchedule(criticalStage, stageTimeSlot);
        await projectRepository.Save(project);
        await eventsPublisher.Publish(new CriticalStagePlanned(projectId, stageTimeSlot, resourceId,
            timeProvider.GetUtcNow().DateTime));
    }

    public async Task PlanCriticalStage(ProjectId projectId, Stage criticalStage, TimeSlot stageTimeSlot)
    {
        var project = await projectRepository.GetById(projectId);
        project.AddSchedule(criticalStage, stageTimeSlot);
        await projectRepository.Save(project);
        await eventsPublisher.Publish(new CriticalStagePlanned(projectId, stageTimeSlot, null,
            timeProvider.GetUtcNow().DateTime));
    }

    public async Task DefineManualSchedule(ProjectId projectId, Schedule schedule)
    {
        var project = await projectRepository.GetById(projectId);
        project.AddSchedule(schedule);
        await projectRepository.Save(project);
    }

    public TimeSpan DurationOf(params Stage[] stages)
    {
        return DurationCalculator.Calculate(stages.ToList());
    }

    public async Task<ProjectCard> Load(ProjectId projectId)
    {
        var project = await projectRepository.GetById(projectId);
        return ToSummary(project);
    }

    public async Task<IList<ProjectCard>> LoadAll(HashSet<ProjectId> projectsIds)
    {
        return (await projectRepository.FindAllByIdIn(projectsIds))
            .Select(ToSummary)
            .ToList();
    }

    public async Task<IList<ProjectCard>> LoadAll()
    {
        return (await projectRepository.FindAll())
            .Select(ToSummary)
            .ToList();
    }

    private ProjectCard ToSummary(Project project)
    {
        return new ProjectCard(project.Id, project.Name, project.ParallelizedStages, project.AllDemands,
            project.Schedule, project.DemandsPerStage, project.ChosenResources);
    }
}