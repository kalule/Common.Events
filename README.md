# Common.Events

A modular .NET class library for integrating event-driven communication using RabbitMQ. It provides an event bus abstraction, auto-registration of event handlers, and a background service for message consumption.

## 💡 Overview

**Common.Events** simplifies the process of publishing and subscribing to integration events in microservices. It supports RabbitMQ as a transport, provides automatic queue declaration and message handling, and integrates easily with ASP.NET Core DI.

## ✨ Features

- `IEventBus` interface for standardized publishing and subscribing
- `RabbitMqEventBus` for RabbitMQ-based message exchange
- Transaction support: `StartTransaction`, `Commit`, and `Rollback`
- `EventConsumerBackgroundService` for auto-subscribing and dispatching messages using reflection
- Lightweight models: `IntegrationEvent`, `EmailRequestedEvent`, `EmailAttachment`
- Extensions for registering event handlers via `ServiceCollectionExtensions`
- Built-in logging with Serilog or Microsoft Logging

## 📦 Installation

Add the project reference to your .NET service:

```bash
dotnet add reference ../Common.Events/Common.Events.csproj
```

## 🔧 Configuration

### `appsettings.json`

```json
"EVENTBUS": {
  "Provider": "RABBITMQ",
  "Servers": [ "localhost" ],
  "UserName": "guest",
  "Password": "guest",
  "ExchangeName": "",
  "QueueName": "",
  "UseDurableExchange": true,
  "Retry": {
    "RequeueOnError": true
  }
}
```

### Registering the Event Bus in Startup

```csharp
services.AddEventBus(Configuration);
services.AddEventBusConsumers(); // auto-registers all handlers
services.AddHostedService<EventConsumerBackgroundService>(); // optional background listener
```

## 📤 Publishing an Event

```csharp
_eventBus.StartTransaction();
_eventBus.Publish(new EmailRequestedEvent {
    Recipients = new List<string> { "user@example.com" },
    Subject = "Test Email",
    BodyHtml = "<p>Hello from Common.Events!</p>"
});
_eventBus.Commit();
```

## 📥 Subscribing to Events

1. Implement a handler:

```csharp
public class EmailRequestedEventHandler : IEventBusIntegrationEventHandler<EmailRequestedEvent>
{
    public async Task HandleAsync(EmailRequestedEvent @event)
    {
        // Logic for handling the email
    }
}
```

2. Ensure your project references `Common.Events` and registers consumers:

```csharp
services.AddEventBusConsumers();
```

## 🧱 Main Components

- `IEventBus`: Contract for publishing and subscribing
- `RabbitMqEventBus`: RabbitMQ implementation
- `EventConsumerBackgroundService`: Auto-subscribes to events from all loaded assemblies
- `IntegrationEvent`: Base event with `EventId` and `CreatedAt`
- `EmailRequestedEvent`: Event model for sending emails with optional attachments
- `BaseEventHandler<T>`: Generic base class for custom handlers
- `ServiceCollectionExtensions`: Registers consumers automatically from assemblies

## 🔄 Serialization

Uses **Newtonsoft.Json** for consistent and flexible JSON handling across the event bus.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file.
