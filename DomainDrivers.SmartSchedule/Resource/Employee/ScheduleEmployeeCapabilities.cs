using DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Resource.Employee;

public class ScheduleEmployeeCapabilities(
    EmployeeRepository employeeRepository,
    CapabilityScheduler capabilityScheduler)
{
    public async Task<IList<AllocatableCapabilityId>> SetupEmployeeCapabilities(EmployeeId employeeId,
        TimeSlot timeSlot)
    {
        var summary = await employeeRepository.FindSummary(employeeId);
        var policy = FindAllocationPolicy(summary);
        var capabilities = policy.SimultaneousCapabilitiesOf(summary);
        return await capabilityScheduler.ScheduleResourceCapabilitiesForPeriod(employeeId.ToAllocatableResourceId(),
            capabilities, timeSlot);
    }

    private IEmployeeAllocationPolicy FindAllocationPolicy(EmployeeSummary employee)
    {
        if (employee.Seniority == Seniority.LEAD)
        {
            return IEmployeeAllocationPolicy.Simultaneous(IEmployeeAllocationPolicy.OneOfSkills(),
                IEmployeeAllocationPolicy.PermissionsInMultipleProjects(3));
        }

        return IEmployeeAllocationPolicy.DefaultPolicy();
    }
}