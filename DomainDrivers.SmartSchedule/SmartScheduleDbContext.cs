using DomainDrivers.SmartSchedule.Allocation;
using DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;
using DomainDrivers.SmartSchedule.Allocation.Cashflow;
using DomainDrivers.SmartSchedule.Resource.Device;
using DomainDrivers.SmartSchedule.Resource.Employee;
using DomainDrivers.SmartSchedule.Risk;
using Microsoft.EntityFrameworkCore;

namespace DomainDrivers.SmartSchedule;

public class SmartScheduleDbContext(DbContextOptions<SmartScheduleDbContext> options) : DbContext(options),
    IAllocationDbContext,
    ICashflowDbContext, IEmployeeDbContext, IDeviceDbContext, ICapabilitySchedulingDbContext, IRiskDbContext
{
    public DbSet<ProjectAllocations> ProjectAllocations { get; set; } = null!;
    public DbSet<Cashflow> Cashflows { get; set; } = null!;
    public DbSet<Employee> Employees { get; set; } = null!;
    public DbSet<Device> Devices { get; set; } = null!;
    public DbSet<AllocatableCapability> AllocatableCapabilities { get; set; } = null!;
    public DbSet<RiskPeriodicCheckSaga> RiskPeriodicCheckSagas { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartScheduleDbContext).Assembly);
    }
}