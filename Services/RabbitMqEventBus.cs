using Common.Events.Interfaces;
using Common.Events.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Common.EventBus.Services
{
    public class RabbitMqEventBus : IEventBus, IDisposable
    {
        private readonly IConnection _connection;
        private IModel _channel;
        private readonly ILogger<RabbitMqEventBus> _logger;
        private readonly RabbitMqOptions _options;
        private readonly IServiceProvider _serviceProvider;

        public RabbitMqEventBus(IOptions<RabbitMqOptions> options, ILogger<RabbitMqEventBus> logger, IServiceProvider serviceProvider)
        {
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.Servers[0], // Consider handling multiple servers
                    UserName = _options.UserName,
                    Password = _options.Password,
                    VirtualHost =  "/" // Use configured virtual host or default
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare exchange and queue on startup to avoid redundant declarations
                _channel.ExchangeDeclare(_options.ExchangeName, "direct", durable: _options.UseDurableExchange);
            }
            catch (BrokerUnreachableException ex)
            {
                _logger.LogError(ex, "RabbitMQ broker unreachable.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing RabbitMQ connection.");
               
            }
        }

        public void StartTransaction()
        {
            try
            {
                _channel.TxSelect();
                _logger.LogInformation("RabbitMQ transaction started.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting RabbitMQ transaction. {ex}" );
                throw;
            }
        }

        public void Publish<T>(T @event) where T : class
        {
            if (_channel == null || _channel.IsClosed)
            {
                _logger.LogError("RabbitMQ channel is closed or null.");
                throw new InvalidOperationException("RabbitMQ channel is closed or null.");
            }

            try
            {
                var eventType = @event.GetType();
                var routingKey = eventType.Name;
                var queueName = routingKey;

                _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind(queue: queueName, exchange: _options.ExchangeName, routingKey: routingKey);

                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event));
                _channel.BasicPublish(_options.ExchangeName, routingKey, null, body);

                _logger.LogInformation("Published {EventType} to {Exchange} => {Queue} ({RoutingKey})",
                    eventType.Name, _options.ExchangeName, queueName, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing event {EventType}.", typeof(T).Name);
                throw;
            }
        }

        public void Commit()
        {
            try
            {
                _channel.TxCommit();
                _logger.LogInformation("RabbitMQ transaction committed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error committing RabbitMQ transaction.");
                throw;
            }
        }

        public void Rollback()
        {
            try
            {
                _channel.TxRollback();
                _logger.LogWarning("RabbitMQ transaction rolled back.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rolling back RabbitMQ transaction.");
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                _channel?.Dispose();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing RabbitMQ resources.");
            }
        }


        public void Subscribe<TEvent>() where TEvent : class
        {
            var eventType = typeof(TEvent);
            var routingKey = eventType.Name;
            var queueName = routingKey;

            _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetService<IEventHandler<TEvent>>();
                if (handler == null)
                {
                    _logger.LogWarning("No handler found for event type {EventType}", typeof(TEvent).Name);
                    return;
                }

                var messageBody = Encoding.UTF8.GetString(ea.Body.ToArray());
                var @event = JsonConvert.DeserializeObject<TEvent>(messageBody);

                if (@event != null)
                {
                    try
                    {
                        await handler.HandleAsync(@event, CancellationToken.None);
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling message of type {EventType}", typeof(TEvent).Name);
                        _channel.BasicNack(ea.DeliveryTag, false, true); // requeue=true
                    }
                }
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("Subscribed to {EventType}", typeof(TEvent).Name);
        }

    }
}