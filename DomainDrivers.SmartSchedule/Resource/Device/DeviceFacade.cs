using DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Resource.Device;

public class DeviceFacade(
    DeviceRepository deviceRepository,
    ScheduleDeviceCapabilities scheduleDeviceCapabilities,
    IUnitOfWork unitOfWork)
{
    public async Task<DeviceSummary> FindDevice(DeviceId deviceId)
    {
        return await deviceRepository.FindSummary(deviceId);
    }

    public async Task<IList<Capability>> FindAllCapabilities()
    {
        return await deviceRepository.FindAllCapabilities();
    }
    
    public async Task<DeviceId> CreateDevice(string model, ISet<Capability> assets)
    {
        return await unitOfWork.InTransaction(async () =>
        {
            var deviceId = DeviceId.NewOne();
            var device = new Device(deviceId, model, assets);
            await deviceRepository.Add(device);
            return deviceId;
        });
    }

    public async Task<IList<AllocatableCapabilityId>> ScheduleCapabilities(DeviceId deviceId, TimeSlot oneDay)
    {
        return await scheduleDeviceCapabilities.SetupDeviceCapabilities(deviceId, oneDay);
    }
}