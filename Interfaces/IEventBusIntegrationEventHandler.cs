using Common.Events.Models;

namespace Common.Events.Interfaces
{
    public interface IEventBusIntegrationEventHandler<TEvent> where TEvent : IntegrationEvent
    {
        Task<IntegrationEvent?> HandleAsync(TEvent eventContext);
    }

    public interface IEventHandler<in TEvent> where TEvent : class
    {
        Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
    }
}

