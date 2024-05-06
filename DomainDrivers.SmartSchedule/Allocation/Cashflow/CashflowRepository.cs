using Microsoft.EntityFrameworkCore;

namespace DomainDrivers.SmartSchedule.Allocation.Cashflow;

public class CashflowRepository(ICashflowDbContext cashflowDbContext) : ICashflowRepository
{
    public async Task<Cashflow?> FindById(ProjectAllocationsId projectId)
    {
        return await cashflowDbContext.Cashflows
            .SingleOrDefaultAsync(x => x.ProjectId == projectId);
    }

    public async Task<Cashflow> GetById(ProjectAllocationsId projectId)
    {
        return await cashflowDbContext.Cashflows
            .SingleAsync(x => x.ProjectId == projectId);
    }

    public async Task<Cashflow> Add(Cashflow cashflow)
    {
        return (await cashflowDbContext.Cashflows.AddAsync(cashflow)).Entity;
    }

    public Task<Cashflow> Update(Cashflow cashflow)
    {
        return Task.FromResult(cashflowDbContext.Cashflows.Update(cashflow).Entity);
    }

    public async Task<IList<Cashflow>> FindAll()
    {
        return await cashflowDbContext.Cashflows.ToListAsync();
    }
}