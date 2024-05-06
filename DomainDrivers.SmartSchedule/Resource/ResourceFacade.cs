using DomainDrivers.SmartSchedule.Resource.Device;
using DomainDrivers.SmartSchedule.Resource.Employee;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Resource;

public class ResourceFacade(EmployeeFacade employeeFacade, DeviceFacade deviceFacade)
{
    public async Task<IList<Capability>> FindAllCapabilities() 
    {
        var employeeCapabilities = await employeeFacade.FindAllCapabilities();
        var deviceCapabilities = await deviceFacade.FindAllCapabilities();
        return deviceCapabilities.Concat(employeeCapabilities).ToList();
    }
}