namespace Common.Events.Models
{
    public class RabbitMqOptions
    {
        public string Provider { get; set; }
        public List<string> Servers { get; set; } = new();
        public string UserName { get; set; }
        public string Password { get; set; }
        public string ExchangeName { get; set; }
        public string QueueName { get; set; }
        public bool UseDurableExchange { get; set; }
        public RetryOptions Retry { get; set; } = new();
    }

    public class RetryOptions
    {
        public bool RequeueOnError { get; set; }
    }
}
