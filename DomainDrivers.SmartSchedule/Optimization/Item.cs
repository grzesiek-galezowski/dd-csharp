namespace DomainDrivers.SmartSchedule.Optimization;

public record Item(string Name, double Value, TotalWeight TotalWeight)
{
    public bool IsWeightZero => TotalWeight.Components().Count == 0;
}