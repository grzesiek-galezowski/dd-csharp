﻿using DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;
using DomainDrivers.SmartSchedule.Allocation.Cashflow;
using DomainDrivers.SmartSchedule.Shared;
using DomainDrivers.SmartSchedule.Simulation;

namespace DomainDrivers.SmartSchedule.Allocation;

public record PotentialTransfers(
    ProjectsAllocationsSummary Summary,
    IDictionary<ProjectAllocationsId, Earnings> Earnings)
{
    public PotentialTransfers Transfer(ProjectAllocationsId projectFrom, ProjectAllocationsId projectTo,
        AllocatedCapability allocatedCapability, TimeSlot forSlot)
    {
        Summary.ProjectAllocations.TryGetValue(projectFrom, out var from);
        Summary.ProjectAllocations.TryGetValue(projectTo, out var to);
        if (from == null || to == null)
        {
            return this;
        }

        var newAllocationsProjectFrom = from.Remove(allocatedCapability.AllocatedCapabilityId, forSlot);
        if (newAllocationsProjectFrom == from)
        {
            return this;
        }

        Summary.ProjectAllocations[projectFrom] = newAllocationsProjectFrom;
        var newAllocationsProjectTo =
            to.Add(allocatedCapability with { TimeSlot = forSlot });
        Summary.ProjectAllocations[projectTo] = newAllocationsProjectTo;
        return new PotentialTransfers(Summary, Earnings);
    }

    public IList<SimulatedProject> ToSimulatedProjects()
    {
        return Summary.ProjectAllocations.Keys.Select(project =>
                new SimulatedProject(ProjectId.From(project.Id), () => Earnings[project].Value, GetMissingDemands(project)))
            .ToList();
    }

    private DomainDrivers.SmartSchedule.Simulation.Demands GetMissingDemands(ProjectAllocationsId projectAllocationsId)
    {
        var allDemands = Summary.Demands[projectAllocationsId]
            .MissingDemands(Summary.ProjectAllocations[projectAllocationsId]);
        return new DomainDrivers.SmartSchedule.Simulation.Demands(
            allDemands
                .All
                .Select(demand => new DomainDrivers.SmartSchedule.Simulation.Demand(demand.Capability, demand.Slot))
                .ToList());
    }
    
    public PotentialTransfers Transfer(ProjectAllocationsId projectTo, AllocatableCapabilitySummary capabilityToTransfer, TimeSlot forSlot)
    {
        var projectToMoveFrom = FindProjectToMoveFrom(capabilityToTransfer.Id, forSlot);
        if (projectToMoveFrom != null)
        {
            return Transfer(projectToMoveFrom, projectTo, new AllocatedCapability(capabilityToTransfer.Id, capabilityToTransfer.Capabilities, capabilityToTransfer.TimeSlot), forSlot);
        }
        return this;
    }

    private ProjectAllocationsId? FindProjectToMoveFrom(AllocatableCapabilityId cap, TimeSlot inSlot)
    {
        return Summary.ProjectAllocations
            .FirstOrDefault(entry => entry.Value.Find(cap) != null)
            .Key;
    }

    public virtual bool Equals(PotentialTransfers? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Summary == other.Summary
               && Earnings.DictionaryEqual(other.Earnings);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Summary, Earnings.CalculateHashCode());
    }
}