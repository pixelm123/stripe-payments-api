using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;
using StripePayments.Application.DTOs;
using StripePayments.Application.Services;
using StripePayments.Domain.Enums;
using StripePayments.Infrastructure.Persistence;
using DomainSubscription = StripePayments.Domain.Entities.Subscription;

namespace StripePayments.Infrastructure.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly AppDbContext _db;
    private readonly Stripe.SubscriptionService _stripeSubscriptions;
    private readonly string _basicPriceId;
    private readonly string _proPriceId;

    public SubscriptionService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        _stripeSubscriptions = new Stripe.SubscriptionService();
        _basicPriceId = configuration["Stripe:BasicPriceId"] ?? string.Empty;
        _proPriceId = configuration["Stripe:ProPriceId"] ?? string.Empty;
    }

    public async Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionRequest request)
    {
        var customer = await _db.Customers.FindAsync(request.CustomerId)
            ?? throw new KeyNotFoundException($"Customer {request.CustomerId} not found.");

        var priceId = request.Plan == SubscriptionPlan.Basic ? _basicPriceId : _proPriceId;

        var stripeSubscription = await _stripeSubscriptions.CreateAsync(new SubscriptionCreateOptions
        {
            Customer = customer.StripeCustomerId,
            Items = [new SubscriptionItemOptions { Price = priceId }],
            PaymentBehavior = "default_incomplete",
            Expand = ["latest_invoice.payment_intent"]
        });

        var subscription = new DomainSubscription
        {
            CustomerId = customer.Id,
            StripeSubscriptionId = stripeSubscription.Id,
            StripePriceId = priceId,
            Plan = request.Plan,
            Status = MapStripeStatus(stripeSubscription.Status),
            CurrentPeriodStart = stripeSubscription.CurrentPeriodStart,
            CurrentPeriodEnd = stripeSubscription.CurrentPeriodEnd
        };

        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync();

        return MapToDto(subscription);
    }

    public async Task<SubscriptionDto> GetSubscriptionAsync(Guid customerId)
    {
        var subscription = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.CustomerId == customerId)
            ?? throw new KeyNotFoundException($"No subscription found for customer {customerId}.");

        return MapToDto(subscription);
    }

    public async Task<SubscriptionDto> CancelSubscriptionAsync(Guid id)
    {
        var subscription = await _db.Subscriptions.FindAsync(id)
            ?? throw new KeyNotFoundException($"Subscription {id} not found.");

        await _stripeSubscriptions.CancelAsync(subscription.StripeSubscriptionId);

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapToDto(subscription);
    }

    internal static SubscriptionStatus MapStripeStatus(string status) => status switch
    {
        "active"   => SubscriptionStatus.Active,
        "past_due" => SubscriptionStatus.PastDue,
        "canceled" => SubscriptionStatus.Cancelled,
        _          => SubscriptionStatus.Incomplete
    };

    private static SubscriptionDto MapToDto(DomainSubscription s) => new(
        s.Id,
        s.CustomerId,
        s.StripeSubscriptionId,
        s.Plan,
        s.Status,
        s.CurrentPeriodStart,
        s.CurrentPeriodEnd,
        s.UpdatedAt
    );
}
