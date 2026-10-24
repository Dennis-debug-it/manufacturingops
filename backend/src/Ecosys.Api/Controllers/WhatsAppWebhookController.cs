using System.Text.Json;
using Ecosys.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The shared Ecosys number's webhook (Release 3 design 4.3). Anonymous: Meta proves itself with the
/// verify token on the GET handshake and the X-Hub-Signature-256 HMAC on every POST. One URL for every
/// tenant, because there is one number; delivery status finds its tenant through the outbox row.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/whatsapp/webhook")]
[EnableRateLimiting(WebhookRateLimitPolicy)]
public sealed class WhatsAppWebhookController(IWhatsAppWebhookProcessor processor, ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    public const string WebhookRateLimitPolicy = "WhatsAppWebhook";
    /// <summary>Meta batches up to about 1,000 updates per call; real payloads are a few KB.</summary>
    public const int MaxBodyBytes = 512 * 1024;

    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (!string.Equals(mode, "subscribe", StringComparison.Ordinal) || !processor.IsVerifyTokenValid(verifyToken) || string.IsNullOrEmpty(challenge))
            return StatusCode(StatusCodes.Status403Forbidden);
        return Content(challenge, "text/plain");
    }

    [HttpPost]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxBodyBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        // The signature covers the exact bytes Meta sent, so read them before any parsing.
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await Request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
                return StatusCode(StatusCodes.Status413PayloadTooLarge);
            buffer.Write(chunk, 0, read);
        }
        var body = buffer.ToArray();

        if (!processor.IsSignatureValid(body, Request.Headers["X-Hub-Signature-256"].ToString()))
        {
            logger.LogWarning("WhatsApp webhook call with a missing or wrong signature refused.");
            return Unauthorized();
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        using (document)
            await processor.HandleAsync(document.RootElement, cancellationToken);
        return Ok();
    }
}
