﻿using DomainDrivers.SmartSchedule.Shared;
using Microsoft.EntityFrameworkCore;

namespace DomainDrivers.SmartSchedule.Resource.Employee;

public class EmployeeRepository(IEmployeeDbContext employeeDbContext)
{
    public async Task<EmployeeSummary> FindSummary(EmployeeId employeeId)
    {
        var employee = await employeeDbContext.Employees.SingleAsync(x => x.Id == employeeId);
        
        var skills = FilterCapabilities(employee.Capabilities, cap => cap.IsOfType("SKILL"));
        var permissions = FilterCapabilities(employee.Capabilities, cap => cap.IsOfType("PERMISSION"));
        return new EmployeeSummary(employeeId, employee.Name, employee.LastName, employee.Seniority, skills, permissions);
    }

    public async Task<IList<Capability>> FindAllCapabilities()
    {
        return (await employeeDbContext.Employees.ToListAsync())
            .SelectMany(employee => employee.Capabilities)
            .ToList();
    }
    
    private ISet<Capability> FilterCapabilities(ISet<Capability> capabilities, Predicate<Capability> predicate)
    {
        return capabilities.Where(capability => predicate(capability)).ToHashSet();
    }

    public async Task Add(Employee employee)
    {
        await employeeDbContext.Employees.AddAsync(employee);
    }
}