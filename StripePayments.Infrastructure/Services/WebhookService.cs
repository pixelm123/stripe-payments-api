using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using StripePayments.Domain.Entities;
using StripePayments.Domain.Enums;
using StripePayments.Infrastructure.Persistence;

namespace StripePayments.Infrastructure.Services;

public class WebhookService
{
    private readonly AppDbContext _db;
    private readonly string _webhookSecret;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(AppDbContext db, IConfiguration configuration, ILogger<WebhookService> logger)
    {
        _db = db;
        _webhookSecret = configuration["Stripe:WebhookSecret"] ?? string.Empty;
        _logger = logger;
    }

    public async Task ProcessAsync(string json, string stripeSignatureHeader)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, stripeSignatureHeader, _webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning("Stripe webhook signature verification failed: {Message}", ex.Message);
            throw new InvalidOperationException("Invalid Stripe signature.", ex);
        }

        // Idempotency check — skip already-processed events
        if (await _db.WebhookEvents.AnyAsync(e => e.StripeEventId == stripeEvent.Id))
        {
            _logger.LogInformation("Duplicate webhook event {EventId} — skipping.", stripeEvent.Id);
            return;
        }

        await HandleEventAsync(stripeEvent);

        _db.WebhookEvents.Add(new WebhookEvent
        {
            StripeEventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            Payload = json
        });

        await _db.SaveChangesAsync();
    }

    private async Task HandleEventAsync(Event stripeEvent)
    {
        switch (stripeEvent.Type)
        {
            case EventTypes.InvoicePaid:
            {
                var invoice = stripeEvent.Data.Object as Invoice;
                if (invoice?.SubscriptionId is null) break;
                await UpdateSubscriptionStatusAsync(invoice.SubscriptionId, SubscriptionStatus.Active, invoice);
                break;
            }

            case EventTypes.InvoicePaymentFailed:
            {
                var invoice = stripeEvent.Data.Object as Invoice;
                if (invoice?.SubscriptionId is null) break;
                await UpdateSubscriptionStatusAsync(invoice.SubscriptionId, SubscriptionStatus.PastDue, invoice);
                break;
            }

            case EventTypes.CustomerSubscriptionDeleted:
            {
                var stripeSubscription = stripeEvent.Data.Object as Stripe.Subscription;
                if (stripeSubscription is null) break;
                await UpdateSubscriptionStatusAsync(stripeSubscription.Id, SubscriptionStatus.Cancelled, null, stripeSubscription);
                break;
            }

            default:
                _logger.LogInformation("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                break;
        }
    }

    private async Task UpdateSubscriptionStatusAsync(
        string stripeSubscriptionId,
        SubscriptionStatus newStatus,
        Invoice? invoice,
        Stripe.Subscription? stripeSubscription = null)
    {
        var subscription = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId);

        if (subscription is null)
        {
            _logger.LogWarning("Webhook: subscription {Id} not found in DB.", stripeSubscriptionId);
            return;
        }

        subscription.Status = newStatus;
        subscription.UpdatedAt = DateTime.UtcNow;

        if (stripeSubscription is not null)
        {
            subscription.CurrentPeriodStart = stripeSubscription.CurrentPeriodStart;
            subscription.CurrentPeriodEnd = stripeSubscription.CurrentPeriodEnd;
        }
        else if (invoice is not null)
        {
            subscription.CurrentPeriodStart = invoice.PeriodStart;
            subscription.CurrentPeriodEnd = invoice.PeriodEnd;
        }
    }
}
