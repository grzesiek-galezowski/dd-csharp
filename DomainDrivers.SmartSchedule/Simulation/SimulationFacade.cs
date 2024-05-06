using DomainDrivers.SmartSchedule.Optimization;

namespace DomainDrivers.SmartSchedule.Simulation;

public class SimulationFacade(OptimizationFacade optimizationFacade)
{
    public double ProfitAfterBuyingNewCapability(IList<SimulatedProject> projectsSimulations,
        SimulatedCapabilities capabilitiesWithoutNewOne, AdditionalPricedCapability newPricedCapability)
    {
        var capabilitiesWithNewResource =
            capabilitiesWithoutNewOne.Add(newPricedCapability.AvailableResourceCapability);
        var resultWithout = optimizationFacade.Calculate(ToItems(projectsSimulations),
            ToCapacity(capabilitiesWithoutNewOne), Comparer<Item>.Create((x, y) => y.Value.CompareTo(x.Value)));
        var resultWith = optimizationFacade.Calculate(ToItems(projectsSimulations),
            ToCapacity(capabilitiesWithNewResource), Comparer<Item>.Create((x, y) => y.Value.CompareTo(x.Value)));
        return resultWith.Profit - decimal.ToDouble(newPricedCapability.Value) - resultWithout.Profit;
    }

    public Result WhatIsTheOptimalSetup(
        IList<SimulatedProject> projectsSimulations, SimulatedCapabilities totalCapability)
    {
        return optimizationFacade.Calculate(ToItems(projectsSimulations), ToCapacity(totalCapability),
            Comparer<Item>.Create((x, y) => y.Value.CompareTo(x.Value)));
    }

    private TotalCapacity ToCapacity(SimulatedCapabilities simulatedCapabilities)
    {
        var capabilities = simulatedCapabilities.Capabilities;
        var capacityDimensions = new List<ICapacityDimension>(capabilities);
        return new TotalCapacity(capacityDimensions);
    }

    private IList<Item> ToItems(IList<SimulatedProject> projectsSimulations)
    {
        return projectsSimulations
            .Select(ToItem)
            .ToList();
    }

    private Item ToItem(SimulatedProject simulatedProject)
    {
        var missingDemands = simulatedProject.MissingDemands.All;
        IList<IWeightDimension> weights = new List<IWeightDimension>(missingDemands);
        return new Item(simulatedProject.ProjectId.ToString(),
            decimal.ToDouble(simulatedProject.CalculateValue()), new TotalWeight(weights));
    }
}