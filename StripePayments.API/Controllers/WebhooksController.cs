using Microsoft.AspNetCore.Mvc;
using StripePayments.Infrastructure.Services;

namespace StripePayments.API.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly WebhookService _webhookService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(WebhookService webhookService, ILogger<WebhooksController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    [HttpPost("stripe")]
    public async Task<IActionResult> StripeWebhook()
    {
        // Read raw body — required for signature verification
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();

        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

        try
        {
            await _webhookService.ProcessAsync(json, signature);
        }
        catch (InvalidOperationException ex)
        {
            // Invalid signature — return 400 so Stripe knows to stop retrying this payload
            _logger.LogWarning("Webhook rejected: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // All other errors: log and return 200 so Stripe doesn't retry endlessly
            _logger.LogError(ex, "Unhandled error processing webhook.");
        }

        // Always return 200 to Stripe for handled and unhandled event types
        return Ok();
    }
}
