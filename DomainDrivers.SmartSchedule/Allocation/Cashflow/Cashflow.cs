namespace DomainDrivers.SmartSchedule.Allocation.Cashflow;

public class Cashflow(ProjectAllocationsId projectId)
{
    public ProjectAllocationsId ProjectId { get; private set; } = projectId;
    private Income? _income;
    private Cost? _cost;

    public Earnings Earnings()
    {
        return _income!.Minus(_cost!);
    }

    public void Update(Income income, Cost cost)
    {
        _income = income;
        _cost = cost;
    }
}