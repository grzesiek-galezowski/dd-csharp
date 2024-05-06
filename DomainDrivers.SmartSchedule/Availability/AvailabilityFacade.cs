using DomainDrivers.SmartSchedule.Availability.Segment;
using DomainDrivers.SmartSchedule.Shared;
using static DomainDrivers.SmartSchedule.Availability.Segment.SegmentInMinutes;

namespace DomainDrivers.SmartSchedule.Availability;

public interface IAvailabilityFacade
{
    Task CreateResourceSlots(ResourceId resourceId, TimeSlot timeslot);

    Task CreateResourceSlots(ResourceId resourceId, ResourceId parentId,
        TimeSlot timeslot);

    Task<bool> Block(ResourceId resourceId, TimeSlot timeSlot, Owner requester);
    Task<bool> Release(ResourceId resourceId, TimeSlot timeSlot, Owner requester);
    Task<bool> Disable(ResourceId resourceId, TimeSlot timeSlot, Owner requester);
    Task<ResourceId?> BlockRandomAvailable(ISet<ResourceId> resourceIds, TimeSlot within, Owner owner);
    Task<ResourceGroupedAvailability> FindGrouped(ResourceId resourceId, TimeSlot within);
    Task<Calendar> LoadCalendar(ResourceId resourceId, TimeSlot within);
    Task<Calendars> LoadCalendars(ISet<ResourceId> resources, TimeSlot within);
    Task<ResourceGroupedAvailability> Find(ResourceId resourceId, TimeSlot within);
    Task<ResourceGroupedAvailability> FindByParentId(ResourceId parentId, TimeSlot within);
}

public class AvailabilityFacade(
    ResourceAvailabilityRepository availabilityRepository,
    ResourceAvailabilityReadModel availabilityReadModel,
    IEventsPublisher eventsPublisher,
    TimeProvider timeProvider,
    IUnitOfWork unitOfWork)
    : IAvailabilityFacade
{
    public async Task CreateResourceSlots(ResourceId resourceId, TimeSlot timeslot)
    {
        var groupedAvailability = ResourceGroupedAvailability.Of(resourceId, timeslot);
        await availabilityRepository.SaveNew(groupedAvailability);
    }

    public async Task CreateResourceSlots(ResourceId resourceId, ResourceId parentId,
        TimeSlot timeslot)
    {
        var groupedAvailability = ResourceGroupedAvailability.Of(resourceId, timeslot, parentId);
        await availabilityRepository.SaveNew(groupedAvailability);
    }

    public async Task<bool> Block(ResourceId resourceId, TimeSlot timeSlot, Owner requester)
    {
        return await unitOfWork.InTransaction(async () =>
        {
            var toBlock = await FindGrouped(resourceId, timeSlot);
            return await Block(requester, toBlock);
        });
    }

    private async Task<bool> Block(Owner requester, ResourceGroupedAvailability toBlock)
    {
        if (toBlock.HasNoSlots)
        {
            return false;
        }
        var result = toBlock.Block(requester);

        if (result)
        {
            return await availabilityRepository.SaveCheckingVersion(toBlock);
        }

        return result;
    }

    public async Task<bool> Release(ResourceId resourceId, TimeSlot timeSlot, Owner requester)
    {
        return await unitOfWork.InTransaction(async () =>
        {
            var toRelease = await FindGrouped(resourceId, timeSlot);
            if (toRelease.HasNoSlots)
            {
                return false;
            }

            var result = toRelease.Release(requester);

            if (result)
            {
                return await availabilityRepository.SaveCheckingVersion(toRelease);
            }

            return result;
        });
    }

    public async Task<bool> Disable(ResourceId resourceId, TimeSlot timeSlot, Owner requester)
    {
        return await unitOfWork.InTransaction(async () =>
        {
            var toDisable = await FindGrouped(resourceId, timeSlot);
            if (toDisable.HasNoSlots)
            {
                return false;
            }

            var previousOwners = toDisable.Owners();
            var result = toDisable.Disable(requester);

            if (result)
            {
                result = await availabilityRepository.SaveCheckingVersion(toDisable);

                if (result)
                {
                    await eventsPublisher.Publish(new ResourceTakenOver(resourceId, previousOwners, timeSlot,
                        timeProvider.GetUtcNow().DateTime));
                }
            }

            return result;
        });
    }

    public async Task<ResourceId?> BlockRandomAvailable(ISet<ResourceId> resourceIds, TimeSlot within, Owner owner)
    {
        return await unitOfWork.InTransaction(async () =>
        {
            var normalized = Segments.NormalizeToSegmentBoundaries(within, DefaultSegment());
            var groupedAvailability =
                await availabilityRepository.LoadAvailabilitiesOfRandomResourceWithin(resourceIds, normalized);

            if (await Block(owner, groupedAvailability))
            {
                return groupedAvailability.ResourceId;
            }
            else
            {
                return null;
            }
        });
    }

    public async Task<ResourceGroupedAvailability> FindGrouped(ResourceId resourceId, TimeSlot within)
    {
        var normalized = Segments.NormalizeToSegmentBoundaries(within, DefaultSegment());
        return new ResourceGroupedAvailability(await availabilityRepository.LoadAllWithinSlot(resourceId, normalized));
    }
    
    public async Task<Calendar> LoadCalendar(ResourceId resourceId, TimeSlot within) {
        var normalized = Segments.NormalizeToSegmentBoundaries(within, DefaultSegment());
        return await availabilityReadModel.Load(resourceId, normalized);
    }

    public async Task<Calendars> LoadCalendars(ISet<ResourceId> resources, TimeSlot within) {
        var normalized = Segments.NormalizeToSegmentBoundaries(within, DefaultSegment());
        return await availabilityReadModel.LoadAll(resources, normalized);
    }

    public async Task<ResourceGroupedAvailability> Find(ResourceId resourceId, TimeSlot within)
    {
        var normalized = Segments.NormalizeToSegmentBoundaries(within, DefaultSegment());
        return new ResourceGroupedAvailability(await availabilityRepository.LoadAllWithinSlot(resourceId, normalized));
    }

    public async Task<ResourceGroupedAvailability> FindByParentId(ResourceId parentId, TimeSlot within)
    {
        var normalized = Segments.NormalizeToSegmentBoundaries(within, DefaultSegment());
        return new ResourceGroupedAvailability(
            await availabilityRepository.LoadAllByParentIdWithinSlot(parentId, normalized));
    }
}