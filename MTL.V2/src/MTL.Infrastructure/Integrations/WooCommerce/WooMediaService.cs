using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Integrations;
using MTL.Infrastructure.Persistence;
using MTL.Infrastructure.Security;

namespace MTL.Infrastructure.Integrations.WooCommerce;

public sealed record WooMediaItem(
    long Id,
    string Url,
    string? ThumbnailUrl,
    string Title,
    string? AltText,
    string? MimeType,
    int? Width,
    int? Height,
    DateTimeOffset? CreatedAt);

public sealed record WooMediaPage(
    IReadOnlyList<WooMediaItem> Items,
    int Total,
    int Page,
    int PageSize,
    bool UploadEnabled = true,
    string Source = "wordpress");

public sealed class WooMediaBridgeUnavailableException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

public interface IWooMediaService
{
    Task<WooMediaPage> GetPageAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);
    Task<WooMediaItem> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default);
}

public sealed class WooMediaService(
    HttpClient httpClient,
    AppDbContext db,
    ISecretProtector secretProtector)
    : IWooMediaService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WooMediaPage> GetPageAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        await ConfigureClientAsync(cancellationToken);
        var query = $"wp-json/wc-mtl/v1/media?page={page}&per_page={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += $"&search={Uri.EscapeDataString(search.Trim())}";
        }

        using var response = await httpClient.GetAsync(query, cancellationToken);
        await EnsureBridgeResponseAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<WooMediaPage>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("WordPress trả về danh sách Media không hợp lệ.");
    }

    public async Task<WooMediaItem> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await ConfigureClientAsync(cancellationToken);
        using var body = new MultipartFormDataContent();
        using var file = new StreamContent(stream);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        body.Add(file, "file", fileName);

        using var response = await httpClient.PostAsync("wp-json/wc-mtl/v1/media", body, cancellationToken);
        await EnsureBridgeResponseAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<WooMediaItem>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("WordPress trả về ảnh vừa tải lên không hợp lệ.");
    }

    private async Task ConfigureClientAsync(CancellationToken cancellationToken)
    {
        var connection = await db.WooCommerceConnections.SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("WooCommerce chưa được cấu hình.");
        if (!connection.IsEnabled)
        {
            throw new InvalidOperationException("WooCommerce đang bị tắt trong phần cấu hình.");
        }

        if (!Uri.TryCreate(connection.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Host != "localhost"))
        {
            throw new InvalidOperationException("WooCommerce URL phải dùng HTTPS.");
        }

        string key;
        string secret;
        try
        {
            key = secretProtector.Unprotect(connection.ConsumerKeyCiphertext);
            secret = secretProtector.Unprotect(connection.ConsumerSecretCiphertext);
        }
        catch (SecretProtectionException exception)
        {
            throw new WooMediaBridgeUnavailableException(
                "Không thể đọc thông tin đăng nhập WooCommerce. Hãy khôi phục bộ khóa Data Protection hoặc nhập lại Consumer Key và Consumer Secret tại Cấu hình > WooCommerce.",
                exception);
        }
        httpClient.BaseAddress = baseUri;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret}")));
    }

    private static async Task EnsureBridgeResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new WooMediaBridgeUnavailableException(
                "Chưa cài plugin MTL Media Bridge trên WordPress. Hãy cài plugin để upload trực tiếp bằng Woo Consumer Key/Secret.");
        }

        if (response.IsSuccessStatusCode) return;

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"WordPress Media trả về lỗi {(int)response.StatusCode}: {TrimDetail(detail)}");
    }

    private static string TrimDetail(string value) => value.Length <= 500 ? value : value[..500];
}
