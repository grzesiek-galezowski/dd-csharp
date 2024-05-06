using DomainDrivers.SmartSchedule.Availability;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;

public interface ICapabilityFinder
{
    Task<AllocatableCapabilitiesSummary> FindAvailableCapabilities(Capability capability,
        TimeSlot timeSlot);

    Task<AllocatableCapabilitiesSummary> FindCapabilities(Capability capability, TimeSlot timeSlot);

    Task<AllocatableCapabilitiesSummary> FindById(IList<AllocatableCapabilityId> allocatableCapabilityIds);

    Task<AllocatableCapabilitySummary?> FindById(AllocatableCapabilityId allocatableCapabilityId);
}

public class CapabilityFinder(
    IAvailabilityFacade availabilityFacade,
    AllocatableCapabilityRepository allocatableResourceRepository)
    : ICapabilityFinder
{
    public async Task<AllocatableCapabilitiesSummary> FindAvailableCapabilities(Capability capability,
        TimeSlot timeSlot)
    {
        var findAllocatableCapability =
            await allocatableResourceRepository.FindByCapabilityWithin(capability.Name, capability.Type, timeSlot.From,
                timeSlot.To);
        var found = await FilterAvailabilityInTimeSlot(findAllocatableCapability, timeSlot);
        return CreateSummary(found);
    }

    public async Task<AllocatableCapabilitiesSummary> FindCapabilities(Capability capability, TimeSlot timeSlot)
    {
        var found = await allocatableResourceRepository.FindByCapabilityWithin(capability.Name, capability.Type,
            timeSlot.From, timeSlot.To);
        return CreateSummary(found);
    }

    public async Task<AllocatableCapabilitiesSummary> FindById(IList<AllocatableCapabilityId> allocatableCapabilityIds)
    {
        var allByIdIn = await allocatableResourceRepository.FindAllById(allocatableCapabilityIds);
        return CreateSummary(allByIdIn);
    }

    public async Task<AllocatableCapabilitySummary?> FindById(AllocatableCapabilityId allocatableCapabilityId)
    {
        var allocatableCapability = await allocatableResourceRepository.FindById(allocatableCapabilityId);
        
        if (allocatableCapability == null)
        {
            return null;
        }

        return CreateSummary(allocatableCapability);
    }

    private async Task<IList<AllocatableCapability>> FilterAvailabilityInTimeSlot(
        IList<AllocatableCapability> findAllocatableCapability, TimeSlot timeSlot)
    {
        var resourceIds =
            findAllocatableCapability
                .Select(ac => ac.Id.ToAvailabilityResourceId())
                .ToHashSet();
        var calendars = await availabilityFacade.LoadCalendars(resourceIds, timeSlot);
        return findAllocatableCapability
            .Where(ac => calendars.CalendarsDictionary[ac.Id.ToAvailabilityResourceId()].AvailableSlots()
                .Contains(timeSlot))
            .ToList();
    }

    private AllocatableCapabilitiesSummary CreateSummary(IList<AllocatableCapability> from)
    {
        return new AllocatableCapabilitiesSummary(from
            .Select(CreateSummary).ToList());
    }

    private AllocatableCapabilitySummary CreateSummary(AllocatableCapability allocatableCapability)
    {
        return new AllocatableCapabilitySummary(allocatableCapability.Id, allocatableCapability.ResourceId,
            allocatableCapability.Capabilities, allocatableCapability.TimeSlot);
    }
}