using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Resource.Employee;

public interface IEmployeeAllocationPolicy
{
    IList<CapabilitySelector> SimultaneousCapabilitiesOf(EmployeeSummary employee);

    public static IEmployeeAllocationPolicy DefaultPolicy()
    {
        return new DefaultPolicy();
    }

    public static IEmployeeAllocationPolicy PermissionsInMultipleProjects(int howMany)
    {
        return new PermissionsInMultipleProjectsPolicy(howMany);
    }

    public static IEmployeeAllocationPolicy OneOfSkills()
    {
        return new OneOfSkillsPolicy();
    }

    public static CompositePolicy Simultaneous(params IEmployeeAllocationPolicy[] policies)
    {
        return new CompositePolicy(policies.ToList());
    }
}

file class DefaultPolicy : IEmployeeAllocationPolicy
{
    public IList<CapabilitySelector> SimultaneousCapabilitiesOf(EmployeeSummary employee)
    {
        var all = new HashSet<Capability>();
        all.UnionWith(employee.Skills);
        all.UnionWith(employee.Permissions);
        return new List<CapabilitySelector> { CapabilitySelector.CanPerformOneOf(all) };
    }
}

file class PermissionsInMultipleProjectsPolicy(int howMany) : IEmployeeAllocationPolicy
{
    public IList<CapabilitySelector> SimultaneousCapabilitiesOf(EmployeeSummary employee)
    {
        return employee.Permissions
            .SelectMany(permission => Enumerable.Range(0, howMany).Select(_ => permission))
            .Select(CapabilitySelector.CanJustPerform)
            .ToList();
    }
}

file class OneOfSkillsPolicy : IEmployeeAllocationPolicy
{
    public IList<CapabilitySelector> SimultaneousCapabilitiesOf(EmployeeSummary employee)
    {
        return new List<CapabilitySelector> { CapabilitySelector.CanPerformOneOf(employee.Skills) };
    }
}

public class CompositePolicy(IList<IEmployeeAllocationPolicy> policies) : IEmployeeAllocationPolicy
{
    public IList<CapabilitySelector> SimultaneousCapabilitiesOf(EmployeeSummary employee)
    {
        return policies
            .SelectMany(p => p.SimultaneousCapabilitiesOf(employee))
            .ToList();
    }
}