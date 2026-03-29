namespace StripePayments.Domain.Entities;

public class WebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StripeEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public string Payload { get; set; } = string.Empty;
}
