using DomainDrivers.SmartSchedule.Allocation;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Planning;

public class CreateProjectAllocations(
    AllocationFacade allocationFacade,
    IProjectRepository projectRepository,
    IUnitOfWork unitOfWork)
{
    private readonly AllocationFacade _allocationFacade = allocationFacade;

    //can react to ScheduleCalculated event
    public async Task Create(ProjectId projectId)
    {
        await unitOfWork.InTransaction(async () =>
        {
            var project = await projectRepository.GetById(projectId);
            var schedule = project.Schedule;
            //for each stage in schedule
            //create allocation
            //allocate chosen resources (or find equivalents)
            //start risk analysis
        });
    }
}