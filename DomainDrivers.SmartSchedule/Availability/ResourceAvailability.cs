using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Availability;

public class ResourceAvailability(
    ResourceAvailabilityId id,
    ResourceId resourceId,
    ResourceId resourceParentId,
    TimeSlot segment,
    Blockade blockade,
    int version)
{
    public ResourceAvailabilityId Id { get; } = id;
    public ResourceId ResourceId { get; } = resourceId;
    public ResourceId ResourceParentId { get; } = resourceParentId;
    public TimeSlot Segment { get; } = segment;
    public Blockade Blockade { get; private set; } = blockade;
    public int Version { get; private set; } = version;

    public ResourceAvailability(ResourceAvailabilityId availabilityId, ResourceId resourceId,
        TimeSlot segment) : this(availabilityId, resourceId, ResourceId.None(), segment, Blockade.None(), 0)
    {
    }

    public ResourceAvailability(ResourceAvailabilityId availabilityId, ResourceId resourceId,
        ResourceId resourceParentId, TimeSlot segment) : this(availabilityId, resourceId, resourceParentId, segment, Blockade.None(), 0)
    {
    }

    public Owner BlockedBy => Blockade.TakenBy;

    public bool IsDisabled => Blockade.Disabled;

    public bool Block(Owner requester)
    {
        if (IsAvailableFor(requester))
        {
            Blockade = Blockade.OwnedBy(requester);
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool Release(Owner requester)
    {
        if (IsAvailableFor(requester))
        {
            Blockade = Blockade.None();
            return true;
        }

        return false;
    }

    public bool Disable(Owner requester)
    {
        Blockade = Blockade.DisabledBy(requester);
        return true;
    }

    public bool Enable(Owner requester)
    {
        if (Blockade.CanBeTakenBy(requester))
        {
            Blockade = Blockade.None();
            return true;
        }

        return false;
    }

    private bool IsAvailableFor(Owner requester)
    {
        return Blockade.CanBeTakenBy(requester) && !IsDisabled;
    }

    public bool IsDisabledBy(Owner owner)
    {
        return Blockade.IsDisabledBy(owner);
    }

    protected bool Equals(ResourceAvailability other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ResourceAvailability)obj);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}