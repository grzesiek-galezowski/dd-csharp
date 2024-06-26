﻿using DomainDrivers.SmartSchedule.Availability;
using DomainDrivers.SmartSchedule.Shared;
using Npgsql;

namespace DomainDrivers.SmartSchedule.Tests.Availability;

public class ResourceAvailabilityUniquenessTest : IntegrationTestWithSharedApp
{
    private static readonly TimeSlot OneMonth = TimeSlot.CreateDailyTimeSlotAtUtc(2021, 1, 1);

    private readonly IResourceAvailabilityRepository _resourceAvailabilityRepository;

    public ResourceAvailabilityUniquenessTest(IntegrationTestApp testApp) : base(testApp)
    {
        _resourceAvailabilityRepository = Scope.ServiceProvider.GetRequiredService<IResourceAvailabilityRepository>();
    }
    
    [Fact]
    public async Task CantSaveTwoAvailabilitiesWithSameResourceIdAndSegment() {
        //given
        var resourceId = ResourceId.NewOne();
        var anotherResourceId = ResourceId.NewOne();
        var resourceAvailabilityId = ResourceAvailabilityId.NewOne();

        //when
        await _resourceAvailabilityRepository.SaveNew(new ResourceAvailability(resourceAvailabilityId, resourceId, OneMonth));

        //expect
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await _resourceAvailabilityRepository.SaveNew(new ResourceAvailability(resourceAvailabilityId, anotherResourceId, OneMonth));
        });
        Assert.Contains("duplicate key", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }
}