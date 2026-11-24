using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MTL.APP.Models;

namespace MTL.APP.Services;

/// <summary>Gửi ZNS mẫu vệ sinh (webhook) + ghi nhật ký DB.</summary>
public class ZnsVeSinhCampaignService
{
	private readonly IDbContextFactory<MTLContext> _contextFactory;
	private const string VeSinhWebhookUrl = "https://n8n.mtl.vn/webhook/zns-vesinh";

	public ZnsVeSinhCampaignService(IDbContextFactory<MTLContext> contextFactory)
	{
		_contextFactory = contextFactory;
	}

	/// <summary>Tag mặc định: tên Laptop, PC, hoặc chứa vệ sinh (không phân biệt hoa thường).</summary>
	public static List<int> ResolveDefaultVeSinhTagIds(IEnumerable<Tag> tags)
	{
		var list = new List<int>();
		foreach (var t in tags)
		{
			if (t?.Id == 0) continue;
			var n = t.Name?.Trim() ?? "";
			if (string.IsNullOrEmpty(n)) continue;
			var lower = n.ToLowerInvariant();
			if (lower == "laptop" || lower == "pc"
			    || lower.Contains("vệ sinh", StringComparison.OrdinalIgnoreCase)
			    || lower.Contains("ve sinh", StringComparison.OrdinalIgnoreCase)
			    || lower.Contains("vesinh", StringComparison.OrdinalIgnoreCase))
				list.Add(t.Id);
		}
		return list.Distinct().ToList();
	}

	/// <summary>Ánh xạ thẻ trên HĐ → tham số &lt;sudungdichvu&gt; (đồng bộ DialogAddZNS).</summary>
	public static string ResolveSudungdichvu(string? listIdTag, IReadOnlyDictionary<int, Tag> tagById)
	{
		if (string.IsNullOrWhiteSpace(listIdTag) || tagById.Count == 0)
			return "Sử dụng dịch vụ";

		foreach (var part in listIdTag.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (!int.TryParse(part, out var id) || !tagById.TryGetValue(id, out var tag))
				continue;
			var n = tag.Name?.Trim() ?? "";
			var lower = n.ToLowerInvariant();
			if (lower.Contains("vệ sinh", StringComparison.OrdinalIgnoreCase)
			    || lower.Contains("ve sinh", StringComparison.OrdinalIgnoreCase)
			    || lower.Contains("vesinh", StringComparison.OrdinalIgnoreCase))
				return "Vệ sinh";
		}
		foreach (var part in listIdTag.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (!int.TryParse(part, out var id) || !tagById.TryGetValue(id, out var tag))
				continue;
			var lower = (tag.Name ?? "").Trim().ToLowerInvariant();
			if (lower == "laptop")
				return "Mua Laptop";
		}
		foreach (var part in listIdTag.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (!int.TryParse(part, out var id) || !tagById.TryGetValue(id, out var tag))
				continue;
			var lower = (tag.Name ?? "").Trim().ToLowerInvariant();
			if (lower == "pc")
				return "Mua PC";
		}
		return "Sử dụng dịch vụ";
	}

	public static string BuildVeSinhPreviewBody(
		string tenkhachhang,
		string sodienthoai,
		string sudungdichvu,
		string orderCode,
		string tenloaimaytinh,
		DateTime? ngayhoadon)
	{
		var ngay = ngayhoadon?.ToString("dd/MM/yyyy") ?? "";
		return
			$"Quý khách {tenkhachhang}, số điện thoại {sodienthoai} đã {sudungdichvu} với số hóa đơn {orderCode} cho {tenloaimaytinh} ngày {ngay} tại Mỹ Tho Laptop. Bạn nên mang máy đến cửa hàng để vệ sinh và kiểm tra nhằm giúp máy hoạt động tốt hơn.\r\n\r\nHẹn gặp quý khách tại Mỹ Tho Laptop !";
	}

	public async Task<(bool Success, int StatusCode, string ResponseBody, string RequestUrl)> SendVeSinhWebhookAsync(
		string phone,
		string customerName,
		string status,
		DateTime ngayGiaoDich,
		string orderCode,
		string ghiChu,
		string loaiDichVu,
		string tenloaimaytinh,
		string nguoiGui,
		CancellationToken ct = default)
	{
		var queryParams = new Dictionary<string, string?>
		{
			["zns-phone"] = phone,
			["zns-tenkhachhang"] = customerName,
			["zns-status"] = status,
			["zns-ngaygiaodich"] = ngayGiaoDich.ToString("dd/MM/yyyy"),
			["zns-order-code"] = orderCode,
			["zns-ghichu"] = (ghiChu ?? "").Replace("\r", "").Replace("\n", "").Replace("\"", "").Trim(),
			["zns-loaidichvu"] = loaiDichVu,
			["zns-nguoiguitin-mtl"] = nguoiGui ?? "",
			["zns-tenloaimaytinh"] = tenloaimaytinh ?? ""
		};
		var url = QueryHelpers.AddQueryString(VeSinhWebhookUrl, queryParams!);

		using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
		using var resp = await http.PostAsync(url, new StringContent("", Encoding.UTF8, "application/json"), ct);
		var body = await resp.Content.ReadAsStringAsync(ct);
		return (resp.IsSuccessStatusCode, (int)resp.StatusCode, body, url);
	}

	public async Task<int> InsertLogAsync(TblZnsGuiNhatKy row, CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(row);
		// EF Core luôn gửi cột Ngaygui trong INSERT. Nếu caller không gán, C# = DateTime.MinValue
		// → SQL lưu 0001-01-01 và KHÔNG áp dụng DEFAULT SYSDATETIME() của bảng.
		if (row.Ngaygui.Year < 1900)
			row.Ngaygui = DateTime.Now;

		await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
		ctx.TblZnsGuiNhatKies.Add(row);
		await ctx.SaveChangesAsync(ct);
		return row.Id;
	}

	/// <summary>Mỗi mã HĐ một bản ghi mới nhất (theo Ngaygui, rồi Id) — phục vụ master-detail danh sách HĐ.</summary>
	public async Task<Dictionary<string, TblZnsGuiNhatKy>> GetLatestZnsLogByMahdAsync(
		int idcompany,
		IEnumerable<string?> mahds,
		CancellationToken ct = default)
	{
		var keys = mahds?
			.Where(s => !string.IsNullOrWhiteSpace(s))
			.Select(s => s!.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList() ?? new List<string>();

		var result = new Dictionary<string, TblZnsGuiNhatKy>(StringComparer.OrdinalIgnoreCase);
		if (keys.Count == 0) return result;

		await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
		var all = await ctx.TblZnsGuiNhatKies.AsNoTracking()
			.Where(z => z.Idcompany == idcompany && keys.Contains(z.Mahd))
			.ToListAsync(ct);

		foreach (var g in all.GroupBy(x => x.Mahd ?? "", StringComparer.OrdinalIgnoreCase))
		{
			if (string.IsNullOrEmpty(g.Key)) continue;
			var best = g.OrderByDescending(x => x.Ngaygui).ThenByDescending(x => x.Id).First();
			result[g.Key] = best;
		}

		return result;
	}

	/// <summary>Bản ghi mới nhất theo từng mã HĐ chỉ cho đúng <paramref name="templateId"/> (tránh nhầm với mẫu khác).</summary>
	public async Task<Dictionary<string, TblZnsGuiNhatKy>> GetLatestZnsLogByMahdAndTemplateAsync(
		int idcompany,
		int templateId,
		IEnumerable<string?> mahds,
		CancellationToken ct = default)
	{
		var keys = mahds?
			.Where(s => !string.IsNullOrWhiteSpace(s))
			.Select(s => s!.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList() ?? new List<string>();

		var result = new Dictionary<string, TblZnsGuiNhatKy>(StringComparer.OrdinalIgnoreCase);
		if (keys.Count == 0) return result;

		await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
		var all = await ctx.TblZnsGuiNhatKies.AsNoTracking()
			.Where(z => z.Idcompany == idcompany && keys.Contains(z.Mahd) && z.Idtemplate == templateId)
			.ToListAsync(ct);

		foreach (var g in all.GroupBy(x => x.Mahd ?? "", StringComparer.OrdinalIgnoreCase))
		{
			if (string.IsNullOrEmpty(g.Key)) continue;
			var best = g.OrderByDescending(x => x.Ngaygui).ThenByDescending(x => x.Id).First();
			result[g.Key] = best;
		}

		return result;
	}

	/// <summary>Toàn bộ nhật ký gửi ZNS của công ty (mới nhất trước), giới hạn số dòng.</summary>
	public async Task<List<TblZnsGuiNhatKy>> GetZnsGuiNhatKyHistoryAsync(
		int idcompany,
		int maxRows = 2000,
		CancellationToken ct = default)
	{
		if (idcompany <= 0) return new List<TblZnsGuiNhatKy>();

		await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
		var take = Math.Clamp(maxRows, 1, 5000);
		return await ctx.TblZnsGuiNhatKies.AsNoTracking()
			.Where(z => z.Idcompany == idcompany)
			.OrderByDescending(z => z.Ngaygui)
			.ThenByDescending(z => z.Id)
			.Take(take)
			.ToListAsync(ct);
	}

	/// <summary>Lấy lịch sử gửi ZNS theo số điện thoại.</summary>
	public async Task<List<TblZnsGuiNhatKy>> GetZnsHistoryByPhoneAsync(
		int idcompany,
		string phone,
		int maxRows = 50,
		CancellationToken ct = default)
	{
		if (idcompany <= 0 || string.IsNullOrWhiteSpace(phone))
			return new List<TblZnsGuiNhatKy>();

		await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
		var take = Math.Clamp(maxRows, 1, 200);
		return await ctx.TblZnsGuiNhatKies.AsNoTracking()
			.Where(z => z.Idcompany == idcompany && z.Sodienthoai == phone)
			.OrderByDescending(z => z.Ngaygui)
			.ThenByDescending(z => z.Id)
			.Take(take)
			.ToListAsync(ct);
	}
}
