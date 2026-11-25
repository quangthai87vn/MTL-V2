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
            ? Ok(new WooConfigurationResponse(null, false, false, false, false, false, null, null))
            : Ok(new WooConfigurationResponse(
                connection.BaseUrl,
                connection.IsEnabled,
                HasCredentials(connection),
                CredentialsAreReadable(connection),
                HasWebhookSecret(connection),
                WebhookSecretIsReadable(connection),
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
        var storedCredentialsAreReadable = connection is not null && CredentialsAreReadable(connection);
        if (connection is null)
        {
            connection = new WooCommerceConnection { CompanyId = companyId };
            db.WooCommerceConnections.Add(connection);
        }

        if (!storedCredentialsAreReadable &&
            (string.IsNullOrWhiteSpace(request.ConsumerKey) || string.IsNullOrWhiteSpace(request.ConsumerSecret)))
        {
            return BadRequest(new
            {
                message = "Consumer Key và Consumer Secret là bắt buộc vì thông tin đăng nhập hiện tại không thể giải mã. Hãy nhập lại cả hai giá trị."
            });
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

        if (!string.IsNullOrWhiteSpace(request.ConsumerKey) && !string.IsNullOrWhiteSpace(request.ConsumerSecret))
        {
            connection.LastError = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new WooConfigurationResponse(
            connection.BaseUrl,
            connection.IsEnabled,
            HasCredentials(connection),
            CredentialsAreReadable(connection),
            HasWebhookSecret(connection),
            WebhookSecretIsReadable(connection),
            connection.LastSuccessfulSyncAtUtc,
            connection.LastError));
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test(CancellationToken cancellationToken)
    {
        try
        {
            var success = await syncService.TestConnectionAsync(cancellationToken);
            return success
                ? Ok(new { message = "Kết nối WooCommerce thành công." })
                : BadRequest(new { message = "Không thể kết nối WooCommerce." });
        }
        catch (WooCommerceRequestException exception)
        {
            return StatusCode((int)exception.StatusCode, new { message = exception.Message, code = exception.ErrorCode });
        }
    }

    [HttpPost("sync/products")]
    public async Task<ActionResult<WooSyncResult>> PullProducts(CancellationToken cancellationToken)
    {
        try
        {
            var result = await syncService.PullAllAsync(cancellationToken);
            return Ok(result);
        }
        catch (WooCommerceRequestException exception)
        {
            return StatusCode((int)exception.StatusCode, new { message = exception.Message, code = exception.ErrorCode });
        }
    }

    private static bool HasCredentials(WooCommerceConnection connection) =>
        !string.IsNullOrWhiteSpace(connection.ConsumerKeyCiphertext) &&
        !string.IsNullOrWhiteSpace(connection.ConsumerSecretCiphertext);

    private static bool HasWebhookSecret(WooCommerceConnection connection) =>
        !string.IsNullOrWhiteSpace(connection.WebhookSecretCiphertext);

    private bool CredentialsAreReadable(WooCommerceConnection connection) =>
        HasCredentials(connection) &&
        CanUnprotect(connection.ConsumerKeyCiphertext) &&
        CanUnprotect(connection.ConsumerSecretCiphertext);

    private bool WebhookSecretIsReadable(WooCommerceConnection connection) =>
        HasWebhookSecret(connection) && CanUnprotect(connection.WebhookSecretCiphertext);

    private bool CanUnprotect(string ciphertext)
    {
        try
        {
            _ = secretProtector.Unprotect(ciphertext);
            return true;
        }
        catch (SecretProtectionException)
        {
            return false;
        }
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
    bool CredentialsReadable,
    bool HasWebhookSecret,
    bool WebhookSecretReadable,
    DateTimeOffset? LastSuccessfulSyncAtUtc,
    string? LastError);

