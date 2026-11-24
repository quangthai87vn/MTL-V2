using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Identity;
using MTL.Infrastructure.Integrations.WooCommerce;
using MTL.Infrastructure.Persistence;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/media")]
public sealed class MediaController(IWooMediaService wooMedia, AppDbContext db) : ControllerBase
{
    private const long MaxFileSize = 15 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    ];

    [Authorize(Policy = Permissions.ProductsRead)]
    [HttpGet]
    public async Task<ActionResult<WooMediaPage>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 60);
        try
        {
            return Ok(await wooMedia.GetPageAsync(page, pageSize, search, cancellationToken));
        }
        catch (WooMediaBridgeUnavailableException)
        {
            var query = db.ProductImages.AsNoTracking()
                .Where(image => image.Product != null);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(image =>
                    (image.Name != null && EF.Functions.ILike(image.Name, $"%{term}%")) ||
                    (image.AltText != null && EF.Functions.ILike(image.AltText, $"%{term}%")) ||
                    EF.Functions.ILike(image.Url, $"%{term}%"));
            }

            var cached = await query
                .OrderByDescending(image => image.UpdatedAtUtc)
                .ToListAsync(cancellationToken);
            var distinct = cached
                .GroupBy(image => image.WooMediaId.HasValue ? $"id:{image.WooMediaId}" : $"url:{image.Url}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            var items = distinct
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(image => new WooMediaItem(
                    image.WooMediaId ?? 0,
                    image.Url,
                    image.ThumbnailUrl ?? image.Url,
                    image.Name ?? "Ảnh sản phẩm",
                    image.AltText,
                    null,
                    null,
                    null,
                    image.CreatedAtUtc))
                .ToList();

            return Ok(new WooMediaPage(items, distinct.Count, page, pageSize, false, "postgresql-cache"));
        }
    }

    [Authorize(Policy = Permissions.ProductsManage)]
    [HttpPost("upload")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<ActionResult<IReadOnlyList<WooMediaItem>>> Upload(
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (files.Count is < 1 or > 20)
        {
            return BadRequest(new { message = "Mỗi lần hãy chọn từ 1 đến 20 ảnh." });
        }

        foreach (var file in files)
        {
            if (file.Length is <= 0 or > MaxFileSize)
            {
                return BadRequest(new { message = $"Ảnh “{file.FileName}” phải nhỏ hơn 15 MB." });
            }

            if (!AllowedTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return BadRequest(new { message = $"Ảnh “{file.FileName}” không đúng định dạng JPG, PNG, WebP hoặc GIF." });
            }
        }

        try
        {
            var uploaded = new List<WooMediaItem>(files.Count);
            foreach (var file in files)
            {
                await using var stream = file.OpenReadStream();
                uploaded.Add(await wooMedia.UploadAsync(
                    stream,
                    Path.GetFileName(file.FileName),
                    file.ContentType,
                    cancellationToken));
            }

            return Ok(uploaded);
        }
        catch (WooMediaBridgeUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }
}
