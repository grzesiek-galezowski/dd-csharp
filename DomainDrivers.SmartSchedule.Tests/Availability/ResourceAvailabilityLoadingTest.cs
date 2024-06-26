﻿using DomainDrivers.SmartSchedule.Availability;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Tests.Availability;

public class ResourceAvailabilityLoadingTest : IntegrationTestWithSharedApp
{
    private static readonly TimeSlot OneMonth = TimeSlot.CreateDailyTimeSlotAtUtc(2021, 1, 1);
    private readonly IResourceAvailabilityRepository _resourceAvailabilityRepository;

    public ResourceAvailabilityLoadingTest(IntegrationTestApp testApp) : base(testApp)
    {
        _resourceAvailabilityRepository = Scope.ServiceProvider.GetRequiredService<IResourceAvailabilityRepository>();
    }

    [Fact]
    public async Task CanSaveAndLoadById()
    {
        //given
        var resourceAvailabilityId = ResourceAvailabilityId.NewOne();
        var resourceId = ResourceId.NewOne();
        var resourceAvailability = new ResourceAvailability(resourceAvailabilityId, resourceId, OneMonth);

        //when
        await _resourceAvailabilityRepository.SaveNew(resourceAvailability);

        //then
        var loaded = await _resourceAvailabilityRepository.LoadById(resourceAvailability.Id);
        Assert.Equal(resourceAvailability, loaded);
        Assert.Equal(resourceAvailability.Segment, loaded.Segment);
        Assert.Equal(resourceAvailability.ResourceId, loaded.ResourceId);
        Assert.Equal(resourceAvailability.BlockedBy, loaded.BlockedBy);
    }
}