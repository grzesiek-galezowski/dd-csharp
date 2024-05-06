using DomainDrivers.SmartSchedule.Resource.Device;
using DomainDrivers.SmartSchedule.Shared;
using static DomainDrivers.SmartSchedule.Shared.Capability;

namespace DomainDrivers.SmartSchedule.Tests.Resource.Device;

public class CreatingDeviceTest : IntegrationTestWithSharedApp
{
    private readonly DeviceFacade _deviceFacade;

    public CreatingDeviceTest(IntegrationTestApp testApp) : base(testApp)
    {
        _deviceFacade = Scope.ServiceProvider.GetRequiredService<DeviceFacade>();
    }
    
    [Fact]
    public async Task CanCreateAndLoadDevices()
    {
        //given
        var device = await _deviceFacade.CreateDevice("super-excavator-1000", Assets("BULLDOZER", "EXCAVATOR"));

        //when
        var loaded = await _deviceFacade.FindDevice(device);

        //then
        Assert.Equal(Assets("BULLDOZER", "EXCAVATOR"), loaded.Assets);
        Assert.Equal("super-excavator-1000", loaded.Model);
    }
    
    [Fact]
    public async Task CanFindAllCapabilities() 
    {
        //given
        await _deviceFacade.CreateDevice("super-excavator-1000", Assets("SMALL-EXCAVATOR", "BULLDOZER"));
        await _deviceFacade.CreateDevice("super-excavator-2000", Assets("MEDIUM-EXCAVATOR", "UBER-BULLDOZER"));
        await _deviceFacade.CreateDevice("super-excavator-3000", Assets("BIG-EXCAVATOR"));

        //when
        var loaded = await _deviceFacade.FindAllCapabilities();

        //then
        Assert.Contains(Asset("SMALL-EXCAVATOR"), loaded);
        Assert.Contains(Asset("BULLDOZER"), loaded);
        Assert.Contains(Asset("MEDIUM-EXCAVATOR"), loaded);
        Assert.Contains(Asset("UBER-BULLDOZER"), loaded);
        Assert.Contains(Asset("BIG-EXCAVATOR"), loaded);
    }
}