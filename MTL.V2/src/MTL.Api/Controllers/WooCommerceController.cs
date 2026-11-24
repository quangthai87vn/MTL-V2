using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Identity;
using MTL.Domain.Integrations;
using MTL.Infrastructure.Identity;
using MTL.Infrastructure.Integrations.WooCommerce;
using MTL.Infrastructure.Persistence;
using MTL.Infrastructure.Security;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/integrations/woocommerce")]
[Authorize(Policy = Permissions.WooCommerceManage)]
public sealed class WooCommerceController(
    AppDbContext db,
    ICurrentUser currentUser,
    ISecretProtector secretProtector,
    IWooProductSyncService syncService)
    : ControllerBase
{
    [HttpGet("configuration")]
    public async Task<ActionResult<WooConfigurationResponse>> GetConfiguration(CancellationToken cancellationToken)
    {
        var connection = await db.WooCommerceConnections.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return connection is null
            ? Ok(new WooConfigurationResponse(null, false, false, false, null, null))
            : Ok(new WooConfigurationResponse(
                connection.BaseUrl,
                connection.IsEnabled,
                !string.IsNullOrEmpty(connection.ConsumerKeyCiphertext),
                !string.IsNullOrEmpty(connection.WebhookSecretCiphertext),
                connection.LastSuccessfulSyncAtUtc,
                connection.LastError));
    }

    [HttpPut("configuration")]
    public async Task<ActionResult<WooConfigurationResponse>> SaveConfiguration(
        SaveWooConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return BadRequest(new { message = "WooCommerce URL phải là địa chỉ HTTPS hợp lệ." });
        }

        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var connection = await db.WooCommerceConnections.SingleOrDefaultAsync(cancellationToken);
        if (connection is null)
        {
            if (string.IsNullOrWhiteSpace(request.ConsumerKey) || string.IsNullOrWhiteSpace(request.ConsumerSecret))
            {
                return BadRequest(new { message = "Woo Key và Woo Secret là bắt buộc khi cấu hình lần đầu." });
            }

            connection = new WooCommerceConnection { CompanyId = companyId };
            db.WooCommerceConnections.Add(connection);
        }

        connection.BaseUrl = request.BaseUrl.TrimEnd('/');
        connection.IsEnabled = request.IsEnabled;
        if (!string.IsNullOrWhiteSpace(request.ConsumerKey))
        {
            connection.ConsumerKeyCiphertext = secretProtector.Protect(request.ConsumerKey.Trim());
        }
        if (!string.IsNullOrWhiteSpace(request.ConsumerSecret))
        {
            connection.ConsumerSecretCiphertext = secretProtector.Protect(request.ConsumerSecret.Trim());
        }
        if (!string.IsNullOrWhiteSpace(request.WebhookSecret))
        {
            connection.WebhookSecretCiphertext = secretProtector.Protect(request.WebhookSecret.Trim());
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new WooConfigurationResponse(
            connection.BaseUrl,
            connection.IsEnabled,
            true,
            !string.IsNullOrWhiteSpace(connection.WebhookSecretCiphertext),
            connection.LastSuccessfulSyncAtUtc,
            connection.LastError));
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test(CancellationToken cancellationToken)
    {
        var success = await syncService.TestConnectionAsync(cancellationToken);
        return success ? Ok(new { message = "Kết nối WooCommerce thành công." }) : BadRequest(new { message = "Không thể kết nối WooCommerce." });
    }

    [HttpPost("sync/products")]
    public async Task<ActionResult<WooSyncResult>> PullProducts(CancellationToken cancellationToken)
    {
        var result = await syncService.PullAllAsync(cancellationToken);
        return Ok(result);
    }
}

public sealed record SaveWooConfigurationRequest(
    string BaseUrl,
    string? ConsumerKey,
    string? ConsumerSecret,
    string? WebhookSecret,
    bool IsEnabled);

public sealed record WooConfigurationResponse(
    string? BaseUrl,
    bool IsEnabled,
    bool HasCredentials,
    bool HasWebhookSecret,
    DateTimeOffset? LastSuccessfulSyncAtUtc,
    string? LastError);

