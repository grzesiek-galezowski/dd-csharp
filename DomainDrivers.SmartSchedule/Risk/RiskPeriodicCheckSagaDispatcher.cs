using DomainDrivers.SmartSchedule.Allocation;
using DomainDrivers.SmartSchedule.Allocation.CapabilityScheduling;
using DomainDrivers.SmartSchedule.Allocation.Cashflow;
using DomainDrivers.SmartSchedule.Availability;
using DomainDrivers.SmartSchedule.Shared;

namespace DomainDrivers.SmartSchedule.Risk;

public class RiskPeriodicCheckSagaDispatcher(
    RiskPeriodicCheckSagaRepository riskSagaRepository,
    PotentialTransfersService potentialTransfersService,
    ICapabilityFinder capabilityFinder,
    IRiskPushNotification riskPushNotification,
    TimeProvider clock,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(ProjectAllocationScheduled @event, CancellationToken cancellationToken)
    {
        var (found, nextStep) = await unitOfWork.InTransaction(async () =>
        {
            var found = await riskSagaRepository.FindByProjectIdOrCreate(@event.ProjectAllocationsId);
            var nextStep = found.Handle(@event);
            return (found, nextStep);
        });
        await Perform(nextStep, found);
    }

    public async Task Handle(NotSatisfiedDemands @event, CancellationToken cancellationToken)
    {
        var nextSteps = await unitOfWork.InTransaction(async () =>
        {
            var sagas = await riskSagaRepository.FindByProjectIdInOrElseCreate(
                [..@event.MissingDemands.Keys]);
            IDictionary<RiskPeriodicCheckSaga, RiskPeriodicCheckSagaStep?> nextSteps =
                new Dictionary<RiskPeriodicCheckSaga, RiskPeriodicCheckSagaStep?>();
            foreach (var saga in sagas)
            {
                var missingDemands = @event.MissingDemands[saga.ProjectId];
                var nextStep = saga.HandleMissingDemands(missingDemands);
                nextSteps[saga] = nextStep;
            }

            return nextSteps;
        });

        foreach (var (saga, nextStep) in nextSteps)
        {
            await Perform(nextStep, saga);
        }
    }

    public async Task Handle(EarningsRecalculated @event, CancellationToken cancellationToken)
    {
        var (found, nextStep) = await unitOfWork.InTransaction(async () =>
        {
            var found = await riskSagaRepository.FindByProjectId(@event.ProjectId);

            if (found == null)
            {
                found = new RiskPeriodicCheckSaga(@event.ProjectId, @event.Earnings);
                await riskSagaRepository.Add(found);
            }

            var nextStep = found.Handle(@event);
            return (found, nextStep);
        });
        await Perform(nextStep, found);
    }

    public async Task Handle(ResourceTakenOver @event, CancellationToken cancellationToken)
    {
        var interested = @event.PreviousOwners
            .Select(owner => new ProjectAllocationsId(owner.OwnerId!.Value))
            .ToList();

        var sagas = await riskSagaRepository.FindByProjectIdIn(interested);

        //transaction per one saga
        foreach (var saga in sagas)
        {
            await Handle(saga, @event);
        }
    }

    private async Task Handle(RiskPeriodicCheckSaga saga, ResourceTakenOver @event)
    {
        var nextStep = await unitOfWork.InTransaction(() =>
        {
            var nextStep = saga.Handle(@event);
            return Task.FromResult(nextStep);
        });
        await Perform(nextStep, saga);
    }

    public async Task HandleWeeklyCheck()
    {
        var sagas = await riskSagaRepository.FindAll();

        foreach (var saga in sagas)
        {
            var nextStep = await unitOfWork.InTransaction(() =>
            {
                var nextStep = saga.HandleWeeklyCheck(clock.GetUtcNow().DateTime);
                return Task.FromResult<RiskPeriodicCheckSagaStep>(nextStep);
            });
            await Perform(nextStep, saga);
        }
    }

    private async Task Perform(RiskPeriodicCheckSagaStep? nextStep, RiskPeriodicCheckSaga saga)
    {
        switch (nextStep)
        {
            case RiskPeriodicCheckSagaStep.NotifyAboutDemandsSatisfied:
                riskPushNotification.NotifyDemandsSatisfied(saga.ProjectId);
                break;
            case RiskPeriodicCheckSagaStep.FindAvailable:
                await HandleFindAvailableFor(saga);
                break;
            case RiskPeriodicCheckSagaStep.DoNothing:
                break;
            case RiskPeriodicCheckSagaStep.SuggestReplacement:
                await HandleSimulateRelocation(saga);
                break;
            case RiskPeriodicCheckSagaStep.NotifyAboutPossibleRisk:
                riskPushNotification.NotifyAboutPossibleRisk(saga.ProjectId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(nextStep), nextStep, null);
        }
    }

    private async Task HandleFindAvailableFor(RiskPeriodicCheckSaga saga)
    {
        var replacements = await FindAvailableReplacementsFor(saga.MissingDemands);

        if (Enumerable.SelectMany<AllocatableCapabilitiesSummary, AllocatableCapabilitySummary>(replacements.Values, x => x.All).Any())
        {
            riskPushNotification.NotifyAboutAvailability(saga.ProjectId, replacements);
        }
    }

    private async Task HandleSimulateRelocation(RiskPeriodicCheckSaga saga)
    {
        var possibleReplacements = await FindPossibleReplacements(saga.MissingDemands);

        foreach (var (demand, replacements) in possibleReplacements)
        {
            foreach (var replacement in replacements.All)
            {
                var profitAfterMovingCapabilities =
                    await potentialTransfersService.ProfitAfterMovingCapabilities(saga.ProjectId, replacement,
                        replacement.TimeSlot);
                if (profitAfterMovingCapabilities > 0)
                {
                    riskPushNotification.NotifyProfitableRelocationFound(saga.ProjectId, replacement.Id);
                }
            }
        }
    }

    private async Task<IDictionary<Demand, AllocatableCapabilitiesSummary>> FindAvailableReplacementsFor(
        Demands demands)
    {
        var replacements = new Dictionary<Demand, AllocatableCapabilitiesSummary>();

        foreach (var demand in demands.All)
        {
            var allocatableCapabilitiesSummary =
                await capabilityFinder.FindAvailableCapabilities(demand.Capability, demand.Slot);
            replacements.Add(demand, allocatableCapabilitiesSummary);
        }

        return replacements;
    }

    private async Task<IDictionary<Demand, AllocatableCapabilitiesSummary>> FindPossibleReplacements(Demands demands)
    {
        var replacements = new Dictionary<Demand, AllocatableCapabilitiesSummary>();

        foreach (var demand in demands.All)
        {
            var allocatableCapabilitiesSummary =
                await capabilityFinder.FindCapabilities(demand.Capability, demand.Slot);
            replacements.Add(demand, allocatableCapabilitiesSummary);
        }

        return replacements;
    }
}