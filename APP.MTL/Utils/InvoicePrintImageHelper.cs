using Microsoft.AspNetCore.Hosting;

namespace MTL.APP.Utils;

/// <summary>
/// Tải ảnh logo phía server → data URL (html2canvas/html2pdf không bị chặn CORS).
/// </summary>
public static class InvoicePrintImageHelper
{
	public static async Task<(string? DataUrl, byte[]? Bytes)> LoadImageAsync(
		string? imageUrl,
		string baseUri,
		IWebHostEnvironment env,
		HttpClient http,
		CancellationToken ct = default)
	{
		var url = imageUrl?.Trim();
		if (string.IsNullOrEmpty(url))
			return (null, null);

		if (url.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
		{
			var bytes = DataUrlToBytes(url);
			return (url, bytes);
		}

		byte[]? bytesOut = null;
		try
		{
			if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)
			    && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
			{
				using var resp = await http.GetAsync(absolute, HttpCompletionOption.ResponseHeadersRead, ct);
				resp.EnsureSuccessStatusCode();
				bytesOut = await resp.Content.ReadAsByteArrayAsync(ct);
			}
			else
			{
				var rel = url.TrimStart('/');
				var physical = Path.Combine(env.WebRootPath, rel.Replace('/', Path.DirectorySeparatorChar));
				if (File.Exists(physical))
					bytesOut = await File.ReadAllBytesAsync(physical, ct);
				else
				{
					var full = new Uri(new Uri(baseUri, UriKind.Absolute), rel).AbsoluteUri;
					using var resp = await http.GetAsync(full, HttpCompletionOption.ResponseHeadersRead, ct);
					resp.EnsureSuccessStatusCode();
					bytesOut = await resp.Content.ReadAsByteArrayAsync(ct);
				}
			}
		}
		catch
		{
			return (null, null);
		}

		if (bytesOut == null || bytesOut.Length == 0)
			return (null, null);

		var mime = GuessMimeFromUrl(url);
		var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytesOut)}";
		return (dataUrl, bytesOut);
	}

	public static byte[]? DataUrlToBytes(string? dataUrl)
	{
		if (string.IsNullOrEmpty(dataUrl))
			return null;
		var idx = dataUrl.IndexOf("base64,", StringComparison.Ordinal);
		if (idx < 0)
			return null;
		try
		{
			return Convert.FromBase64String(dataUrl[(idx + 7)..]);
		}
		catch
		{
			return null;
		}
	}

	static string GuessMimeFromUrl(string url)
	{
		var u = url.ToLowerInvariant();
		if (u.Contains(".png", StringComparison.Ordinal)) return "image/png";
		if (u.Contains(".jpg", StringComparison.Ordinal) || u.Contains(".jpeg", StringComparison.Ordinal)) return "image/jpeg";
		if (u.Contains(".gif", StringComparison.Ordinal)) return "image/gif";
		if (u.Contains(".webp", StringComparison.Ordinal)) return "image/webp";
		return "image/png";
	}
}
