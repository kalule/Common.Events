using Common.EventBus.Services;
using Common.Events.Interfaces;
using Common.Events.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Common.Events.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RabbitMqOptions>(options => configuration.GetSection("EVENTBUS").Bind(options));
            services.AddSingleton<IEventBus, RabbitMqEventBus>();
            return services;
        }

        public static IServiceCollection AddEventBusConsumers(
            this IServiceCollection services,
            IEnumerable<Assembly>? assembliesToScan = null,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("EventBusConsumerScanner");

            var handlerInterfaceTypeDefinition = typeof(IEventBusIntegrationEventHandler<>);

            if (assembliesToScan == null || !assembliesToScan.Any())
            {
                assembliesToScan = new[] { Assembly.GetCallingAssembly() };
            }

            var handlerTypes = assembliesToScan
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException ex)
                    {
                        logger.LogWarning("Could not load types from assembly {Assembly}. Errors: {Errors}", a.FullName, string.Join(", ", ex.LoaderExceptions.Select(e => e?.Message ?? "N/A")));
                        return Array.Empty<Type>();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to get types from assembly {Assembly}", a.FullName);
                        return Array.Empty<Type>();
                    }
                })
                .Select(t => new
                {
                    HandlerType = t,
                    HandlerInterface = t.GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceTypeDefinition)
                })
                .Where(x =>
                    x.HandlerType != null &&
                    !x.HandlerType.IsAbstract &&
                    !x.HandlerType.IsGenericTypeDefinition &&
                    x.HandlerInterface != null)
                .ToList();

            foreach (var item in handlerTypes)
            {
                if (item.HandlerInterface == null || item.HandlerType == null) continue;

                services.Add(new ServiceDescriptor(item.HandlerInterface, item.HandlerType, lifetime));

                logger.LogInformation("Registered Event Handler: {Interface} -> {Implementation} ({Lifetime})",
                    item.HandlerInterface.Name,
                    item.HandlerType.Name,
                    lifetime);
            }

            return services;
        }
    }
}
