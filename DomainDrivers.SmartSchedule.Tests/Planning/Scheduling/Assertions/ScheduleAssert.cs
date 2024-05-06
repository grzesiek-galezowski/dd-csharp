using DomainDrivers.SmartSchedule.Planning.Scheduling;

namespace DomainDrivers.SmartSchedule.Tests.Planning.Scheduling.Assertions;

public class ScheduleAssert(Schedule actual)
{
    public Schedule Schedule { get; } = actual;

    public static ScheduleAssert AssertThat(Schedule actual)
    {
        return new ScheduleAssert(actual);
    }

    public ScheduleAssert HasStages(int number)
    {
        Assert.Equal(number, Schedule.Dates.Count);
        return this;
    }

    public StageAssert HasStage(string name)
    {
        Schedule.Dates.TryGetValue(name, out var stageTimeSlot);
        Assert.NotNull(stageTimeSlot);
        return new StageAssert(stageTimeSlot, this);
    }

    public void IsEmpty()
    {
        Assert.True(Schedule.None() == Schedule);
    }
}