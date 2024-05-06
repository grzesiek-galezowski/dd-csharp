using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Tests.Planning.Scheduling.Assertions;

public class StageAssert(TimeSlot actual, ScheduleAssert scheduleAssert)
{
    public StageAssert ThatStarts(string start)
    {
        Assert.Equal(DateTime.Parse(start), actual.From);
        return this;
    }

    public StageAssert WithSlot(TimeSlot slot)
    {
        Assert.Equal(slot, actual);
        return this;
    }

    public StageAssert ThatEnds(string end)
    {
        Assert.Equal(DateTime.Parse(end), actual.To);
        return this;
    }

    public ScheduleAssert And()
    {
        return scheduleAssert;
    }

    public StageAssert IsBefore(string stage)
    {
        var schedule = scheduleAssert.Schedule;
        Assert.True(actual.To <= schedule.Dates[stage].From);
        return this;
    }

    public StageAssert StartsTogetherWith(string stage)
    {
        var schedule = scheduleAssert.Schedule;
        Assert.Equal(actual.From, schedule.Dates[stage].From);
        return this;
    }

    public StageAssert IsAfter(string stage)
    {
        var schedule = scheduleAssert.Schedule;
        Assert.True(actual.From >= schedule.Dates[stage].To);
        return this;
    }
}