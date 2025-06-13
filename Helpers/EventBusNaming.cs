namespace Common.Events.Helpers
{
    public static class EventBusNaming
    {
        public static string GetRoutingKey<T>() => typeof(T).Name;
        public static string GetQueueName<T>() => $"EventQueue.{typeof(T).Name}";
        public static string GetRoutingKey(Type type) => type.Name;
        public static string GetQueueName(Type type) => $"EventQueue.{type.Name}";
    }

}

