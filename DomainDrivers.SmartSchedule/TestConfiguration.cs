namespace DomainDrivers.SmartSchedule;

public static class TestConfiguration
{
    private static readonly AsyncLocal<Action<IConfigurationBuilder>> Current = new()
    {
        Value = c => { }
    };

    internal static void AddTestConfiguration(this IConfigurationBuilder configurationBuilder)
    {
        Current.Value!(configurationBuilder);
    }

    public static void Set(Action<IConfigurationBuilder> action)
    {
        Current.Value = action;
    }
}