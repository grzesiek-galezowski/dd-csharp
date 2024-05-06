using DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Resource.Employee;

public class EmployeeFacade(
    EmployeeRepository employeeRepository,
    ScheduleEmployeeCapabilities scheduleEmployeeCapabilities,
    IUnitOfWork unitOfWork)
{
    public async Task<EmployeeSummary> FindEmployee(EmployeeId employeeId)
    {
        return await employeeRepository.FindSummary(employeeId);
    }

    public async Task<IList<Capability>> FindAllCapabilities()
    {
        return await employeeRepository.FindAllCapabilities();
    }

    public async Task<EmployeeId> AddEmployee(string name, string lastName, Seniority seniority,
        ISet<Capability> skills, ISet<Capability> permissions)
    {
        return await unitOfWork.InTransaction(async () =>
        {
            var employeeId = EmployeeId.NewOne();
            var capabilities = skills.Concat(permissions).ToHashSet();
            var employee = new Employee(employeeId, name, lastName, seniority, capabilities);
            await employeeRepository.Add(employee);
            return employeeId;
        });
    }

    public async Task<IList<AllocatableCapabilityId>> ScheduleCapabilities(EmployeeId employeeId, TimeSlot oneDay)
    {
        return await scheduleEmployeeCapabilities.SetupEmployeeCapabilities(employeeId, oneDay);
    }

    //add vacation
    // calls availability
    //add sick leave
    // calls availability
    //change skills
}