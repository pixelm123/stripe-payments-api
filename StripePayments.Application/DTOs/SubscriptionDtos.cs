using StripePayments.Domain.Enums;

namespace StripePayments.Application.DTOs;

public record CreateSubscriptionRequest(Guid CustomerId, SubscriptionPlan Plan);

public record SubscriptionDto(
    Guid Id,
    Guid CustomerId,
    string StripeSubscriptionId,
    SubscriptionPlan Plan,
    SubscriptionStatus Status,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    DateTime UpdatedAt
);
