using DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;
using DomainDrivers.SmartSchedule.Availability;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Allocation;

public class AllocationFacade(
    IProjectAllocationsRepository projectAllocationsRepository,
    IAvailabilityFacade availabilityFacade,
    ICapabilityFinder capabilityFinder,
    IEventsPublisher eventsPublisher,
    TimeProvider timeProvider,
    IUnitOfWork unitOfWork)
{
    public async Task<ProjectAllocationsId> CreateAllocation(TimeSlot timeSlot, Demands scheduledDemands)
    {
        return await unitOfWork.InTransaction(async () =>
        {
            var projectId = ProjectAllocationsId.NewOne();
            var projectAllocations = new ProjectAllocations(projectId, Allocations.None(), scheduledDemands, timeSlot);
            await projectAllocationsRepository.Add(projectAllocations);
            await eventsPublisher.Publish(new ProjectAllocationScheduled(projectId, timeSlot,
                timeProvider.GetUtcNow().DateTime));
            return projectId;
        });
    }

    public async Task<ProjectsAllocationsSummary> FindAllProjectsAllocations(ISet<ProjectAllocationsId> projectIds)
    {
        return ProjectsAllocationsSummary.Of(await projectAllocationsRepository.FindAllById(projectIds));
    }

    public async Task<ProjectsAllocationsSummary> FindAllProjectsAllocations()
    {
        return ProjectsAllocationsSummary.Of(await projectAllocationsRepository.FindAll());
    }

    public async Task<Guid?> AllocateToProject(ProjectAllocationsId projectId,
        AllocatableCapabilityId allocatableCapabilityId, TimeSlot timeSlot)
    {
        return await unitOfWork.InTransaction<Guid?>(async () =>
        {
            //yes, one transaction crossing 2 modules.
            var capability = await capabilityFinder.FindById(allocatableCapabilityId);
            if (capability == null)
            {
                return null;
            }

            if (!await availabilityFacade.Block(allocatableCapabilityId.ToAvailabilityResourceId(), timeSlot,
                    Owner.Of(projectId.Id)))
            {
                return null;
            }

            var @event = await Allocate(projectId, allocatableCapabilityId, capability.Capabilities, timeSlot);
            if (@event == null)
            {
                return null;
            }

            return @event.AllocatedCapabilityId;
        });
    }

    private async Task<CapabilitiesAllocated?> Allocate(ProjectAllocationsId projectId,
        AllocatableCapabilityId allocatableCapabilityId, CapabilitySelector capability, TimeSlot timeSlot)
    {
        var allocations = await projectAllocationsRepository.GetById(projectId);
        var @event = allocations.Allocate(allocatableCapabilityId, capability, timeSlot,
            timeProvider.GetUtcNow().DateTime);
        await projectAllocationsRepository.Update(allocations);
        return @event;
    }

    public async Task<bool> ReleaseFromProject(ProjectAllocationsId projectId,
        AllocatableCapabilityId allocatableCapabilityId, TimeSlot timeSlot)
    {
        return await unitOfWork.InTransaction(async () =>
        {
            //can release not scheduled capability - at least for now. Hence no check to capabilityFinder
            await availabilityFacade.Release(allocatableCapabilityId.ToAvailabilityResourceId(), timeSlot,
                Owner.Of(projectId.Id));
            var allocations = await projectAllocationsRepository.GetById(projectId);
            var @event = allocations.Release(allocatableCapabilityId, timeSlot, timeProvider.GetUtcNow().DateTime);
            await projectAllocationsRepository.Update(allocations);
            return @event != null;
        });
    }

    public async Task<bool> AllocateCapabilityToProjectForPeriod(ProjectAllocationsId projectId, Capability capability,
        TimeSlot timeSlot)
    {
        return await unitOfWork.InTransaction(async () =>
        {
            var proposedCapabilities = await capabilityFinder.FindCapabilities(capability, timeSlot);

            if (proposedCapabilities.All.Count == 0)
            {
                return false;
            }

            var availabilityResourceIds = proposedCapabilities.All
                .Select(resource => resource.Id.ToAvailabilityResourceId())
                .ToHashSet();
            var chosen =
                await availabilityFacade.BlockRandomAvailable(availabilityResourceIds, timeSlot,
                    Owner.Of(projectId.Id));

            if (chosen == null)
            {
                return false;
            }

            var toAllocate = FindChosenAllocatableCapability(proposedCapabilities, chosen);
            return await Allocate(projectId, toAllocate.Id, toAllocate.Capabilities, timeSlot) != null;
        });
    }

    private AllocatableCapabilitySummary FindChosenAllocatableCapability(AllocatableCapabilitiesSummary proposedCapabilities,
        ResourceId chosen)
    {
        return proposedCapabilities.All
            .First(summary => summary.Id.ToAvailabilityResourceId() == chosen);
    }

    public async Task EditProjectDates(ProjectAllocationsId projectId, TimeSlot fromTo)
    {
        await unitOfWork.InTransaction(async () =>
        {
            var projectAllocations = await projectAllocationsRepository.GetById(projectId);
            var projectDatesSet = projectAllocations.DefineSlot(fromTo, timeProvider.GetUtcNow().DateTime);
            if (projectDatesSet != null)
            {
                await eventsPublisher.Publish(projectDatesSet);
            }

            await projectAllocationsRepository.Update(projectAllocations);
        });
    }

    public async Task ScheduleProjectAllocationDemands(ProjectAllocationsId projectId, Demands demands)
    {
        await unitOfWork.InTransaction(async () =>
        {
            var projectAllocations = await projectAllocationsRepository.FindById(projectId);
            if (projectAllocations == null)
            {
                projectAllocations = ProjectAllocations.Empty(projectId);
                await projectAllocationsRepository.Add(projectAllocations);
            }
            else
            {
                await projectAllocationsRepository.Update(projectAllocations);
            }

            var @event = projectAllocations.AddDemands(demands, timeProvider.GetUtcNow().DateTime);
            //event could be stored in a local store
            //always remember about transactional boundaries
        });
    }
}