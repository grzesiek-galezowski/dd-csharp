namespace DomainDrivers.SmartSchedule.Tests;

[Collection(nameof(SharedIntegrationTestAppCollection))]
public abstract class IntegrationTestWithSharedApp(IntegrationTestAppBase app) : IntegrationTest(app);

public abstract class IntegrationTest(IntegrationTestAppBase app) : IDisposable
{
    // One scope per test to match java's behavior.

    protected IServiceScope Scope { get; } = app.Services.CreateScope();

    public void Dispose()
    {
        Scope.Dispose();
    }
}