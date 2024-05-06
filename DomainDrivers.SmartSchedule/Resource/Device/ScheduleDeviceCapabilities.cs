using DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;
using DomainDrivers.SmartSchedule.Shared;
using static DomainDrivers.SmartSchedule.Shared.CapabilitySelector;

namespace DomainDrivers.SmartSchedule.Resource.Device;

public class ScheduleDeviceCapabilities(DeviceRepository deviceRepository, CapabilityScheduler capabilityScheduler)
{
    public async Task<IList<AllocatableCapabilityId>> SetupDeviceCapabilities(DeviceId deviceId, TimeSlot timeSlot)
    {
        var summary = await deviceRepository.FindSummary(deviceId);
        return await capabilityScheduler.ScheduleResourceCapabilitiesForPeriod(deviceId.ToAllocatableResourceId(),
            new List<CapabilitySelector> { CanPerformAllAtTheTime(summary.Assets) }, timeSlot);
    }
}