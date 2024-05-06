using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule;

public class UnitOfWork(SmartScheduleDbContext dbContext) : IUnitOfWork
{
    public async Task<T> InTransaction<T>(Func<Task<T>> operation)
    {
        if (dbContext.Database.CurrentTransaction != null)
        {
            return await operation();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            var result = await operation();

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    public async Task InTransaction(Func<Task> operation)
    {
        if (dbContext.Database.CurrentTransaction != null)
        {
            await operation();
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            await operation();

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}