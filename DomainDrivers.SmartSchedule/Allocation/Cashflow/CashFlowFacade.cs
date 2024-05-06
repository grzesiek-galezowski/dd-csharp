using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Allocation.Cashflow;

public class CashFlowFacade(
    ICashflowRepository cashflowRepository,
    IEventsPublisher eventsPublisher,
    TimeProvider timeProvider,
    IUnitOfWork unitOfWork)
{
    public async Task AddIncomeAndCost(ProjectAllocationsId projectId, Income income, Cost cost)
    {
        await unitOfWork.InTransaction(async () =>
        {
            var cashflow = await cashflowRepository.FindById(projectId);
            if (cashflow == null)
            {
                cashflow = new Cashflow(projectId);
                await cashflowRepository.Add(cashflow);
            }

            cashflow.Update(income, cost);
            await cashflowRepository.Update(cashflow);
            await eventsPublisher.Publish(new EarningsRecalculated(projectId, cashflow.Earnings(),
                timeProvider.GetUtcNow().DateTime));
        });
    }

    public async Task<Earnings> Find(ProjectAllocationsId projectId)
    {
        var byId = await cashflowRepository.GetById(projectId);
        return byId.Earnings();
    }

    public async Task<IDictionary<ProjectAllocationsId, Earnings>> FindAllEarnings()
    {
        return (await cashflowRepository.FindAll())
            .ToDictionary(x => x.ProjectId, x => x.Earnings());
    }
}