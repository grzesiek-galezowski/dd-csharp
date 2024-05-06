using DomainDrivers.SmartSchedule.Allocation;
using DomainDrivers.SmartSchedule.Planning.Parallelization;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Planning;

public class EditStageDateService(
    AllocationFacade allocationFacade,
    IProjectRepository projectRepository,
    IUnitOfWork unitOfWork)
{
    private readonly AllocationFacade _allocationFacade = allocationFacade;

    public async Task EditStageDate(ProjectId projectId, Stage stage, TimeSlot timeSlot)
    {
        await unitOfWork.InTransaction(async () =>
        {
            var project = await projectRepository.GetById(projectId);
            var schedule = project.Schedule;
            //redefine schedule
            //for each stage in schedule
            //recreate allocation
            //reallocate chosen resources (or find equivalents)
            //start risk analysis
        });
    }
}