using Common.Events.Interfaces;
using Microsoft.Extensions.Logging;

namespace Common.Events.Models
{
    public abstract class BaseEventHandler<TEvent> : IEventBusIntegrationEventHandler<TEvent>
        where TEvent : IntegrationEvent
    {
        protected readonly ILogger<BaseEventHandler<TEvent>> Logger;

        protected BaseEventHandler(ILogger<BaseEventHandler<TEvent>> logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IntegrationEvent?> HandleAsync(TEvent eventContext)
        {
            if (eventContext == null)
            {
                Logger.LogWarning("Received null event context for type {EventType}", typeof(TEvent).Name);
                throw new ArgumentNullException(nameof(eventContext));
            }

            Logger.LogDebug("Handling {EventType} (ID: {EventId})", typeof(TEvent).Name, eventContext.EventId);
            try
            {
                var processedSuccessfully = await ProcessEventsAsync(eventContext);

                if (!processedSuccessfully)
                {
                    Logger.LogError("Failed to process {EventType} with ID {EventId}", typeof(TEvent).Name, eventContext.EventId);
                    throw new EventProcessingException($"Processing of {typeof(TEvent).Name} failed. Event will be re-queued.");
                }

                Logger.LogInformation("Successfully handled {EventType}", typeof(TEvent).Name);
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception while handling {EventType}", typeof(TEvent).Name);
                throw;
            }
        }

        protected abstract Task<bool> ProcessEventsAsync(TEvent eventData);
    }

    public class EventProcessingException : Exception
    {
        public EventProcessingException(string message) : base(message) { }
    }
}