using System.Text.Json.Serialization;

namespace Common.Events.Models
{
    public class EmailRequestedEvent : IntegrationEvent
    {
        [JsonPropertyName("recipients")]
        public List<string> Recipients { get; set; } = new();

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = default!;

        [JsonPropertyName("bodyHtml")]
        public string BodyHtml { get; set; } = default!;

        [JsonPropertyName("attachments")]
        public List<EmailAttachment> Attachments { get; set; } = new();
    }

    public class EmailAttachment
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = default!;

        [JsonPropertyName("fileBase64Content")]
        public string FileBase64Content { get; set; } = default!;

        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; }
    }
}
