using MediatR;

namespace DomainDrivers.SmartSchedule.Shared;

public interface IEventsPublisher
{
    //remember about transactions scope
    Task Publish(IPublishedEvent @event);
}

public class EventsPublisher(IMediator mediator) : IEventsPublisher
{
    public async Task Publish(IPublishedEvent @event)
    {
        await mediator.Publish(@event);
    }
}