using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using MTL.APP.Models;

namespace MTL.APP.Services
{
	public class ThuHangHoaService
	{
		private readonly MTLContext _context;
		public ThuHangHoaService(MTLContext appDBContext)
		{
			_context = appDBContext;
		}

		/// <summary>
		/// Tạo mã phiếu: ưu tiên gọi SP CreateMaThuHangHoa; nếu lỗi thì sinh mã theo ngày + số thứ tự.
		/// Không dùng MTLContext.CreateMaThuHangHoa() trực tiếp (DbFunction chỉ hợp lệ trong truy vấn EF).
		/// </summary>
		private async Task<string> LayMaPhieuMoiAsync(string maTuDong = "TH")
		{
			var conn = _context.Database.GetDbConnection();
			var shouldClose = conn.State != System.Data.ConnectionState.Open;
			if (shouldClose)
				await conn.OpenAsync();
			try
			{
				await using var cmd = conn.CreateCommand();
				cmd.CommandText = "EXEC [dbo].[CreateMaThuHangHoa] @MaTuDong";
				cmd.Parameters.Add(new SqlParameter("@MaTuDong", maTuDong));
				await using var reader = await cmd.ExecuteReaderAsync();
				if (await reader.ReadAsync() && reader.FieldCount > 0 && !reader.IsDBNull(0))
					return reader.GetString(0).Replace(" ", string.Empty);
			}
			catch
			{
				// SP chưa có hoặc lỗi → fallback C#
			}
			finally
			{
				if (shouldClose && conn.State == System.Data.ConnectionState.Open)
					await conn.CloseAsync();
			}

			return await SinhMaPhieuFallbackAsync(maTuDong);
		}

		private async Task<string> SinhMaPhieuFallbackAsync(string maTuDong)
		{
			var now = DateTime.Now;
			var prefix = $"{maTuDong}{now:yyyyMMdd}";
			var maxSuffix = await _context.TblThuHangHoas
				.AsNoTracking()
				.Where(x => x.Maphieu != null && x.Maphieu.StartsWith(prefix))
				.Select(x => x.Maphieu)
				.ToListAsync();

			var next = 1;
			foreach (var m in maxSuffix)
			{
				if (m != null && m.Length >= prefix.Length + 3)
				{
					var tail = m.AsSpan(prefix.Length, 3);
					if (int.TryParse(tail, out var n) && n >= next)
						next = n + 1;
				}
			}
			return prefix + next.ToString("D3");
		}

		public async Task<List<MTL_GetAllThuHangHoaResult>> GetAllAsync(int idcompany)
		{
			var sql = "EXEC [dbo].[uf_GetAllThuHangHoa] @idcompany = " + idcompany;
			return await Task.FromResult(_context.Set<MTL_GetAllThuHangHoaResult>().FromSqlRaw(sql).ToList());
		}

		public async Task<TblThuHangHoa> GetByIdAsync(int id)
		{
			return await _context.TblThuHangHoas.FirstOrDefaultAsync(x => x.Id == id) ?? new TblThuHangHoa();
		}

		/// <summary>
		/// Tạo phiếu thu + trả về entity đã có Id (EF auto-increment gán vào phieu.Id sau SaveChangesAsync).
		/// Ưu tiên gọi SP CreateMaThuHangHoa; nếu lỗi thì sinh mã theo ngày + số thứ tự.
		/// </summary>
		public async Task<TblThuHangHoa> CreateAsync(TblThuHangHoa phieu)
		{
			// 1. Sinh mã phiếu
			var maPhieu = await LayMaPhieuMoiAsync("TH");
			if (string.IsNullOrWhiteSpace(maPhieu))
				maPhieu = "TH" + DateTime.Now.ToString("yyyyMMddHHmmss");
			phieu.Maphieu = maPhieu;
			phieu.Beginupdate = DateTime.Now;
			phieu.Lastupdate = DateTime.Now;

			// 2. Lưu — Id tự động tăng được gán vào phieu.Id sau SaveChangesAsync
			_context.TblThuHangHoas.Add(phieu);
			await _context.SaveChangesAsync();

			return phieu;
		}

		public async Task UpdateAsync(TblThuHangHoa phieu)
		{
			var existing = await _context.TblThuHangHoas.FirstOrDefaultAsync(x => x.Id == phieu.Id);
			if (existing == null) return;

			existing.Ngaythu = phieu.Ngaythu;
			existing.Idkhachhang = phieu.Idkhachhang;
			existing.Tongtienthu = phieu.Tongtienthu;
			existing.Giamgia = phieu.Giamgia;
			existing.Thanhtoan = phieu.Thanhtoan;
			existing.IsThanhToan = phieu.IsThanhToan;
			existing.Ghichu = phieu.Ghichu;
			existing.Lastupdate = DateTime.Now;

			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(int id)
		{
			var phieu = await _context.TblThuHangHoas.FirstOrDefaultAsync(x => x.Id == id);
			if (phieu != null)
			{
				_context.TblThuHangHoas.Remove(phieu);
				await _context.SaveChangesAsync();
			}
		}

		public async Task<List<TblThuHangHoaChiTiet>> GetChiTietByPhieuAsync(int idphieu)
		{
			return await _context.TblThuHangHoaChiTiets
				.Where(x => x.Idphieu == idphieu)
				.OrderByDescending(x => x.Id)
				.ToListAsync();
		}

		/// <summary>
		/// Lấy toàn bộ dòng chi tiết cho nhiều phiếu trong một truy vấn (phục vụ master-detail trên lưới).
		/// </summary>
		public async Task<Dictionary<int, List<TblThuHangHoaChiTiet>>> GetChiTietByPhieuIdsAsync(IEnumerable<int> phieuIds)
		{
			var ids = phieuIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
			if (ids.Count == 0)
				return new Dictionary<int, List<TblThuHangHoaChiTiet>>();

			var rows = await _context.TblThuHangHoaChiTiets
				.AsNoTracking()
				.Where(x => x.Idphieu != null && ids.Contains(x.Idphieu.Value))
				.ToListAsync();

			return rows
				.GroupBy(x => x.Idphieu!.Value)
				.ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).ToList());
		}

		public async Task AddChiTietAsync(TblThuHangHoaChiTiet item)
		{
			item.Beginupdate = DateTime.Now;
			item.Lastupdate = DateTime.Now;
			_context.TblThuHangHoaChiTiets.Add(item);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateChiTietAsync(TblThuHangHoaChiTiet item)
		{
			var existing = await _context.TblThuHangHoaChiTiets.FirstOrDefaultAsync(x => x.Id == item.Id);
			if (existing == null) return;
			existing.Tensanpham = item.Tensanpham;
			existing.Motasanpham = item.Motasanpham;
			existing.Soluong = item.Soluong;
			existing.Dongia = item.Dongia;
			existing.Thanhtien = item.Thanhtien;
			existing.Idtinhtrang = item.Idtinhtrang;
			existing.Ghichu = item.Ghichu;
			existing.Lastupdate = DateTime.Now;
			await _context.SaveChangesAsync();
		}

		public async Task DeleteChiTietAsync(int id)
		{
			var item = await _context.TblThuHangHoaChiTiets.FirstOrDefaultAsync(x => x.Id == id);
			if (item != null)
			{
				_context.TblThuHangHoaChiTiets.Remove(item);
				await _context.SaveChangesAsync();
			}
		}

		public async Task DeleteChiTietByPhieuAsync(int idphieu)
		{
			var items = await _context.TblThuHangHoaChiTiets.Where(x => x.Idphieu == idphieu).ToListAsync();
			if (items.Count > 0)
			{
				_context.TblThuHangHoaChiTiets.RemoveRange(items);
				await _context.SaveChangesAsync();
			}
		}

		public async Task<List<ThuHangHoaTinhtrang>> GetTinhTrangList()
		{
			return new List<ThuHangHoaTinhtrang>
			{
				new() { Id = 1, Tentrangthai = "Mới thu" },
				new() { Id = 2, Tentrangthai = "Đã phân loại" },
				new() { Id = 3, Tentrangthai = "Đã thanh lý" },
				new() { Id = 4, Tentrangthai = "Hoàn trả khách" },
				new() { Id = 5, Tentrangthai = "Hủy" }
			};
		}

		public async Task<List<MTL_ThongKeThuHangHoaResult>> ThongKeTuanAsync(int idcompany, int nam, int tuan)
		{
			var sql = $"EXEC [dbo].[uf_ThongKeThuHangHoa_Tuan] @idcompany={idcompany}, @nam={nam}, @tuan={tuan}";
			return await Task.FromResult(_context.Set<MTL_ThongKeThuHangHoaResult>().FromSqlRaw(sql).ToList());
		}

		public async Task<List<MTL_ThongKeThuHangHoaResult>> ThongKeThangAsync(int idcompany, int nam, int thang)
		{
			var sql = $"EXEC [dbo].[uf_ThongKeThuHangHoa_Thang] @idcompany={idcompany}, @nam={nam}, @thang={thang}";
			return await Task.FromResult(_context.Set<MTL_ThongKeThuHangHoaResult>().FromSqlRaw(sql).ToList());
		}

		public async Task<List<MTL_ThongKeThuHangHoaResult>> ThongKeNamAsync(int idcompany, int nam)
		{
			var sql = $"EXEC [dbo].[uf_ThongKeThuHangHoa_Nam] @idcompany={idcompany}, @nam={nam}";
			return await Task.FromResult(_context.Set<MTL_ThongKeThuHangHoaResult>().FromSqlRaw(sql).ToList());
		}

		/// <summary>
		/// Sửa lỗi: SP uf_GetAllThuHangHoa JOIN dbo.TblUser (không tồn tại)
		/// thay bằng dbo.tbl_users (bảng thực tế map từ EF TblUser).
		/// Lấy thông tin khách hàng từ bảng tbl_users thông qua Idkhachhang.
		/// Safe: chỉ DROP/CREATE procedure, không ảnh hưởng data.
		/// </summary>
		public async Task<string> PatchStoredProcedureAsync()
		{
			var sql = @"
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'uf_GetAllThuHangHoa' AND type IN ('P', 'FN'))
    DROP PROCEDURE dbo.uf_GetAllThuHangHoa;
EXEC('
CREATE PROCEDURE dbo.uf_GetAllThuHangHoa
    @idcompany INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        thh.Id                           AS id,
        thh.Maphieu                      AS maphieu,
        thh.Ngaythu                      AS ngaythu,
        thh.Idkhachhang                  AS idkhachhang,
        ISNULL(kh.Fullname, '''')        AS tenkhachhang,
        kh.Dienthoai                     AS sdtkhachhang,
        kh.Diachi                        AS diachi,
        kh.Makh                          AS cmndcccd,
        thh.Tongtienthu                  AS tongtienthu,
        thh.Giamgia                      AS giamgia,
        thh.Thanhtoan                    AS thanhtoan,
        thh.IsThanhToan                  AS isThanhToan,
        thh.Idcompany                    AS idcompany,
        thh.Idnguoithu                   AS idnguoithu,
        thh.Ghichu                       AS ghichu,
        thh.Beginupdate                  AS beginupdate,
        thh.Lastupdate                   AS lastupdate,
        u.fullname                       AS tennhanvien
    FROM dbo.TblThuHangHoa thh
    LEFT JOIN dbo.tbl_users u ON thh.Idnguoithu = u.id
    LEFT JOIN dbo.tbl_users kh ON thh.Idkhachhang = kh.id
    WHERE (@idcompany IS NULL OR thh.Idcompany = @idcompany)
    ORDER BY thh.Ngaythu DESC, thh.Id DESC;
END;
');";
			await _context.Database.ExecuteSqlRawAsync(sql);
			return "Đã sửa lỗi uf_GetAllThuHangHoa thành công!";
		}
	}
}
