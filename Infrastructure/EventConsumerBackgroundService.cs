using Common.Events.Interfaces;
using Common.Events.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Common.Events.Infrastructure
{
    public class EventConsumerBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EventConsumerBackgroundService> _logger;
        private readonly RabbitMqOptions _options;
        private IModel _channel;
        private IConnection _connection;

        public EventConsumerBackgroundService(
            IServiceProvider serviceProvider,
            IOptions<RabbitMqOptions> options,
            ILogger<EventConsumerBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options.Value;

            var factory = new ConnectionFactory
            {
                HostName = _options.Servers.FirstOrDefault() ?? "localhost",
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = "/"
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() =>
            {
                var handlerTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Array.Empty<Type>(); }
                    })
                    .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
                    .Select(t => new
                    {
                        HandlerType = t,
                        InterfaceType = t.GetInterfaces()
                            .FirstOrDefault(i => i.IsGenericType &&
                                                 i.GetGenericTypeDefinition() == typeof(IEventBusIntegrationEventHandler<>))
                    })
                    .Where(x => x.InterfaceType != null)
                    .ToList();

                foreach (var handler in handlerTypes)
                {
                    var eventType = handler.InterfaceType!.GetGenericArguments()[0];

                    if (eventType.Name == "TEvent")
                    {
                        _logger.LogWarning("Skipping generic placeholder handler: {Handler}", handler.HandlerType.FullName);
                        continue;
                    }

                    var routingKey = eventType.Name;
                    var queueName = routingKey;

                    _channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Direct, durable: _options.UseDurableExchange);
                    _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
                    _channel.QueueBind(queue: queueName, exchange: _options.ExchangeName, routingKey: routingKey);

                    var consumer = new EventingBasicConsumer(_channel);
                    consumer.Received += async (_, ea) =>
                    {
                        var message = Encoding.UTF8.GetString(ea.Body.ToArray());

                        try
                        {
                            var eventInstance = JsonConvert.DeserializeObject(message, eventType);

                            if (eventInstance == null)
                            {
                                _logger.LogError("Deserialization returned null for {EventType}. Message: {Message}", eventType.Name, message);
                                _channel.BasicNack(ea.DeliveryTag, false, _options.Retry.RequeueOnError);
                                return;
                            }

                            using var scope = _serviceProvider.CreateScope();
                            var handlerInterface = typeof(IEventBusIntegrationEventHandler<>).MakeGenericType(eventType);
                            var handlerInstance = scope.ServiceProvider.GetRequiredService(handlerInterface);
                            var handleMethod = handlerInterface.GetMethod("HandleAsync");

                            if (handleMethod != null)
                            {
                                await (Task)handleMethod.Invoke(handlerInstance, new[] { eventInstance })!;
                                _channel.BasicAck(ea.DeliveryTag, false);
                            }
                            else
                            {
                                _logger.LogError("HandleAsync method not found on handler for {EventType}", eventType.Name);
                                _channel.BasicNack(ea.DeliveryTag, false, _options.Retry.RequeueOnError);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Exception while handling message from queue {QueueName}", queueName);
                            _channel.BasicNack(ea.DeliveryTag, false, _options.Retry.RequeueOnError);
                        }
                    };

                    _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
                    _logger.LogInformation("Listening on {QueueName} for event {EventType}", queueName, eventType.Name);
                }

                _logger.LogInformation("Event consumer background service initialized.");
            }, stoppingToken);
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}
