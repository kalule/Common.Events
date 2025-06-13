using System.Text.Json.Serialization;

public abstract class IntegrationEvent
{
    [JsonPropertyName("eventId")]
    public Guid EventId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
