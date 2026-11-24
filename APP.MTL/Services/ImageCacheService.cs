using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MTL.APP.IServices;

namespace MTL.APP.Services;

public interface IImageCacheService
{
    Task<string?> GetCachedImageAsync(string url);
    Task CacheImageAsync(string url, byte[] imageData);
    Task<byte[]?> GetServerCachedImageAsync(string url);
    bool IsCached(string url);
    string HashUrl(string url);
    string GetCacheFilePath(string hash);
    Task<byte[]?> DownloadAndCacheAsync(string url);
}

public class ImageCacheService : IImageCacheService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageCacheService> _logger;
    private readonly string _cacheDir;

    private static readonly ConcurrentDictionary<string, bool> _memoryCache = new();
    private static readonly ConcurrentDictionary<string, Task<byte[]?>> _downloadTasks = new();

    public ImageCacheService(IHttpClientFactory httpClientFactory, ILogger<ImageCacheService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "woo-cache");

        if (!Directory.Exists(_cacheDir))
            Directory.CreateDirectory(_cacheDir);
    }

    public string HashUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(url.ToLowerInvariant().Trim());
        var hashBytes = sha.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes)[..32].ToLowerInvariant();
    }

    public string GetCacheFilePath(string hash)
        => Path.Combine(_cacheDir, $"{hash}.cache");

    public bool IsCached(string url)
    {
        var hash = HashUrl(url);
        if (_memoryCache.TryGetValue(hash, out var cached))
            return cached;

        var filePath = GetCacheFilePath(hash);
        var exists = File.Exists(filePath);
        if (exists)
            _memoryCache.TryAdd(hash, true);
        return exists;
    }

    public async Task<string?> GetCachedImageAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var hash = HashUrl(url);
        var filePath = GetCacheFilePath(hash);

        if (!File.Exists(filePath))
            return null;

        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            var ext = Path.GetExtension(new Uri(url).AbsolutePath);
            if (string.IsNullOrEmpty(ext) || ext.Length > 6)
                ext = ".jpg";
            var mime = ext.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".bmp" => "image/bmp",
                _ => "image/jpeg"
            };
            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi đọc cache file: {Hash}", hash);
            return null;
        }
    }

    public async Task CacheImageAsync(string url, byte[] imageData)
    {
        if (string.IsNullOrWhiteSpace(url) || imageData == null || imageData.Length == 0)
            return;

        try
        {
            var hash = HashUrl(url);
            var filePath = GetCacheFilePath(hash);
            await File.WriteAllBytesAsync(filePath, imageData);
            _memoryCache.TryAdd(hash, true);
            _logger.LogDebug("Đã cache ảnh: {Url} → {Hash}", url, hash);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi ghi cache file cho: {Url}", url);
        }
    }

    public async Task<byte[]?> GetServerCachedImageAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var hash = HashUrl(url);
        var filePath = GetCacheFilePath(hash);

        if (!File.Exists(filePath)) return null;

        try
        {
            return await File.ReadAllBytesAsync(filePath);
        }
        catch
        {
            return null;
        }
    }

    public async Task<byte[]?> DownloadAndCacheAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var hash = HashUrl(url);
        var filePath = GetCacheFilePath(hash);

        if (File.Exists(filePath))
        {
            try { return await File.ReadAllBytesAsync(filePath); }
            catch { }
        }

        if (_downloadTasks.TryGetValue(hash, out var existingTask))
            return await existingTask;

        var downloadTask = Task.Run(async () =>
        {
            try
            {
                using var client = _httpClientFactory.CreateClient("WooCommerce") ?? new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; MTL-APP/1.0)");
                client.Timeout = TimeSpan.FromSeconds(15);

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("HTTP {Status} khi tải ảnh: {Url}", response.StatusCode, url);
                    _downloadTasks.TryRemove(hash, out _);
                    return (byte[]?)null;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length > 0)
                    await CacheImageAsync(url, bytes);

                _downloadTasks.TryRemove(hash, out _);
                return bytes;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi tải ảnh từ WordPress: {Url}", url);
                _downloadTasks.TryRemove(hash, out _);
                return (byte[]?)null;
            }
        });

        _downloadTasks.TryAdd(hash, downloadTask);
        return await downloadTask;
    }
}
