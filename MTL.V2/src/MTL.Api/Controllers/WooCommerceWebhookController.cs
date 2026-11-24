using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Integrations;
using MTL.Infrastructure.Persistence;
using MTL.Infrastructure.Security;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/webhooks/woocommerce")]
public sealed class WooCommerceWebhookController(
    AppDbContext db,
    ISecretProtector secretProtector,
    ILogger<WooCommerceWebhookController> logger)
    : ControllerBase
{
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost("{companyId:guid}/products")]
    public async Task<IActionResult> ReceiveProductWebhook(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken);

        var connection = await db.WooCommerceConnections.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.CompanyId == companyId && !x.IsDeleted && x.IsEnabled, cancellationToken);
        if (connection is null || string.IsNullOrWhiteSpace(connection.WebhookSecretCiphertext))
        {
            return Unauthorized();
        }

        var signature = Request.Headers["X-WC-Webhook-Signature"].ToString();
        var secret = secretProtector.Unprotect(connection.WebhookSecretCiphertext);
        if (!IsValidSignature(body, secret, signature))
        {
            logger.LogWarning("Rejected WooCommerce webhook with invalid signature for company {CompanyId}", companyId);
            return Unauthorized();
        }

        using var document = JsonDocument.Parse(body);
        var wooProductId = document.RootElement.TryGetProperty("id", out var idElement)
            ? idElement.GetInt64()
            : 0;
        var topic = Request.Headers["X-WC-Webhook-Topic"].ToString();
        var eventId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{topic}:{body}")));

        var exists = await db.SyncJobs.IgnoreQueryFilters()
            .AnyAsync(x => x.CompanyId == companyId && x.ExternalId == eventId, cancellationToken);
        if (!exists)
        {
            db.SyncJobs.Add(new SyncJob
            {
                CompanyId = companyId,
                Type = $"WooWebhook:{topic}",
                ExternalId = eventId,
                Status = SyncJobStatus.Queued,
                DetailsJson = JsonSerializer.Serialize(new { wooProductId, topic })
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return Accepted(new { queued = !exists });
    }

    private static bool IsValidSignature(string body, string secret, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        try
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            var actual = Convert.FromBase64String(signature);
            return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

