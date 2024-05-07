using Quartz;

namespace DomainDrivers.SmartSchedule.Risk;

public class RiskPeriodicCheckSagaWeeklyCheckJob(MediatrRiskPeriodicCheckSagaDispatcher riskPeriodicCheckSagaDispatcher)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await riskPeriodicCheckSagaDispatcher.HandleWeeklyCheck();
    }
}