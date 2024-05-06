using Quartz;

namespace DomainDrivers.SmartSchedule.Allocation;

public class PublishMissingDemandsJob(PublishMissingDemandsService publishMissingDemandsService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await publishMissingDemandsService.Publish();
    }
}