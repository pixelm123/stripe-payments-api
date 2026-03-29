using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StripePayments.Application.DTOs;
using StripePayments.Application.Services;

namespace StripePayments.API.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequest request)
    {
        try
        {
            var subscription = await _subscriptionService.CreateSubscriptionAsync(request);
            return CreatedAtAction(nameof(GetSubscription), new { customerId = subscription.CustomerId }, subscription);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{customerId:guid}")]
    public async Task<IActionResult> GetSubscription(Guid customerId)
    {
        try
        {
            var subscription = await _subscriptionService.GetSubscriptionAsync(customerId);
            return Ok(subscription);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelSubscription(Guid id)
    {
        try
        {
            var subscription = await _subscriptionService.CancelSubscriptionAsync(id);
            return Ok(subscription);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
