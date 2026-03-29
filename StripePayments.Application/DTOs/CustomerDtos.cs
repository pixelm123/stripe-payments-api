namespace StripePayments.Application.DTOs;

public record CreateCustomerRequest(string Email, string Name);

public record CustomerDto(
    Guid Id,
    string StripeCustomerId,
    string Email,
    string Name,
    DateTime CreatedAt,
    SubscriptionDto? Subscription
);
