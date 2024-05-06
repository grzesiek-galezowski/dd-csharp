using Microsoft.EntityFrameworkCore;

namespace DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;

public class AllocatableCapabilityRepository(ICapabilitySchedulingDbContext capabilitySchedulingDbContext)
{
    public async Task<IList<AllocatableCapability>> FindByCapabilityWithin(string name, string type, DateTime from,
        DateTime to)
    {
        return await capabilitySchedulingDbContext.AllocatableCapabilities.FromSql(
                $"""
                 SELECT ac.*
                 FROM allocatable_capabilities ac
                 CROSS JOIN LATERAL jsonb_array_elements(ac.possible_capabilities -> 'capabilities') AS o(obj)
                 WHERE
                     o.obj ->> 'name' = {name}
                     AND o.obj ->> 'type' = {type}
                     AND ac.from_date <= {from}
                     AND ac.to_date >= {to}
                 """)
            .ToListAsync();
    }

    public async Task<AllocatableCapability?> FindByResourceIdAndCapabilityAndTimeSlot(Guid allocatableResourceId,
        string name, string type, DateTime from, DateTime to)
    {
        return await capabilitySchedulingDbContext.AllocatableCapabilities.FromSql(
                $"""
                 SELECT ac.*
                 FROM allocatable_capabilities ac
                 CROSS JOIN LATERAL jsonb_array_elements(ac.possible_capabilities -> 'capabilities') AS o(obj)
                 WHERE
                     ac.resource_id = {allocatableResourceId}
                     AND o.obj ->> 'name' = {name}
                     AND o.obj ->> 'type' = {type}
                     AND ac.from_date = {from}
                     AND ac.to_date = {to}
                 """)
            .SingleOrDefaultAsync();
    }

    public async Task<IList<AllocatableCapability>> FindByResourceIdAndTimeSlot(Guid allocatableResourceId,
        DateTime from, DateTime to)
    {
        return await capabilitySchedulingDbContext.AllocatableCapabilities.FromSql(
                $"""
                 SELECT ac.*
                 FROM allocatable_capabilities ac
                 WHERE
                     ac.resource_id = {allocatableResourceId}
                     AND ac.from_date = {from}
                     AND ac.to_date = {to}
                 """)
            .ToListAsync();
    }

    public async Task<IList<AllocatableCapability>> FindAllById(IList<AllocatableCapabilityId> allocatableCapabilityIds)
    {
        return await capabilitySchedulingDbContext.AllocatableCapabilities
            .Where(x => allocatableCapabilityIds.Contains(x.Id))
            .ToListAsync();
    }
    
    public async Task<AllocatableCapability?> FindById(AllocatableCapabilityId allocatableCapabilityId)
    {
        return await capabilitySchedulingDbContext.AllocatableCapabilities
            .FindAsync(allocatableCapabilityId);
    }

    public async Task SaveAll(IList<AllocatableCapability> allocatableResources)
    {
        await capabilitySchedulingDbContext.AllocatableCapabilities.AddRangeAsync(allocatableResources);
    }

    public async Task<bool> ExistsById(AllocatableCapabilityId id)
    {
        return await capabilitySchedulingDbContext.AllocatableCapabilities.AnyAsync(x => x.Id == id);
    }
}