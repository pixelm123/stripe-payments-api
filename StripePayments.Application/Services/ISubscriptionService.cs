using StripePayments.Application.DTOs;

namespace StripePayments.Application.Services;

public interface ISubscriptionService
{
    Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionRequest request);
    Task<SubscriptionDto> GetSubscriptionAsync(Guid customerId);
    Task<SubscriptionDto> CancelSubscriptionAsync(Guid id);
}
