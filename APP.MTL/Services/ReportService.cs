using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MTL.APP.Models;
using MTL.APP;

namespace MTL.APP.Services
{
	public sealed class ThuTheoNguonThoiGianSummary
	{
		public double TienMat { get; set; }
		public double TienChuyenKhoan { get; set; }
		public double TienChuyenKhoanDuKien { get; set; }
	}

	/// <summary>Một lát cắt cho biểu đồ nguồn tiền đã vào (TM / từng TK NH / CK chưa gán).</summary>
	public sealed class ThuDaVaoNguonSlice
	{
		public string Key { get; init; } = "";
		public string Label { get; init; } = "";
		public double Amount { get; init; }
	}

	/// <summary>Tiền mặt + CK theo từng tài khoản ngân hàng đã cấu hình trong tháng.</summary>
	public sealed class ThuDaVaoTheoNguonResult
	{
		public IReadOnlyList<ThuDaVaoNguonSlice> Slices { get; init; } = Array.Empty<ThuDaVaoNguonSlice>();
	}

	/// <summary>Một ngày: số tiền theo từng nguồn (thứ tự trùng <see cref="ThuDaVaoTheoNguonResult.Slices"/> đã lọc).</summary>
	public sealed class ThuDaVaoTheoNguonNgayRow
	{
		public DateTime Ngay { get; init; }
		public IReadOnlyList<double> AmountsBySlice { get; init; } = Array.Empty<double>();
		public double TongNgay => AmountsBySlice.Sum();
	}

	/// <summary>Một ngày nhập hàng: ngày + tổng tiền nhập.</summary>
	public sealed class NhapHangTheoNgayRow
	{
		public DateTime Ngay { get; init; }
		public double TongNgay { get; init; }
	}

	/// <summary>Bảng nhập hàng theo ngày trong tháng.</summary>
	public sealed class NhapHangTheoNgayResult
	{
		public IReadOnlyList<NhapHangTheoNgayRow> Rows { get; init; } = Array.Empty<NhapHangTheoNgayRow>();
	}

	/// <summary>Bảng nguồn tiền theo ngày trong tháng (cùng logic key với biểu đồ nguồn tháng).</summary>
	public sealed class ThuDaVaoTheoNguonTheoNgayResult
	{
		public IReadOnlyList<string> SourceKeys { get; init; } = Array.Empty<string>();
		public IReadOnlyList<string> SourceLabels { get; init; } = Array.Empty<string>();
		public IReadOnlyList<ThuDaVaoTheoNguonNgayRow> Rows { get; init; } = Array.Empty<ThuDaVaoTheoNguonNgayRow>();
	}

	/// <summary>Một dòng trong bảng sản phẩm bán chạy: tổng hợp theo Idsanpham.</summary>
	public sealed class TopSanPhamRow
	{
		public int? Idsanpham { get; init; }
		public string Tensp { get; init; } = "";
		public string Masp { get; init; } = "";
		public double TongSl { get; init; }
		public double TongDoanhThu { get; init; }
		public double GiaTb { get; init; }
		public int SoDon { get; init; }
	}

	/// <summary>Sắp xếp top sản phẩm theo gì.</summary>
	public enum TopSanPhamSortBy { SoLuong, DoanhThu }

	public class ReportService
	{
		private readonly MTLContext _context;
		public ReportService(MTLContext appDBContext)
		{
			_context = appDBContext;
		}

		/// <summary>
		/// Doanh thu theo ngày trong tháng: chỉ phiếu <b>Hóa đơn</b> và <b>Dịch vụ</b>, trạng thái <b>Đã xử lý</b>,
		/// loại trừ báo giá. Theo <c>Ngaytraphieu</c>. Không dùng SP <c>MTL_ThongKeDoanhThu</c> để đồng bộ logic lọc.
		/// Trả góp: ghi nhận <b>đủ giá trị giao dịch</b> vào ngày trả phiếu (TM + CK đã thu + phần còn lại / CK dự kiến), khớp nguồn tiền theo ngày.
		/// </summary>
		public async Task<List<MTL_ThongKeDoanhThuResult>> GetDoanhThuAsync(int idcompany, int Nam, int Thang, bool isTienThucCo)
		{
			if (idcompany <= 0 || Nam < 1 || Thang < 1 || Thang > 12)
				return new List<MTL_ThongKeDoanhThuResult>();

			var tu = new DateTime(Nam, Thang, 1).Date;
			var den = tu.AddMonths(1).AddTicks(-1);

			var rows = await DoanhThuBaseQuery(idcompany)
				.Where(h => h.Ngaytraphieu >= tu && h.Ngaytraphieu <= den)
				.ToListAsync();

			return rows
				.GroupBy(h => h.Ngaytraphieu!.Value.Date)
				.OrderBy(g => g.Key)
				.Select(g =>
				{
					var hh = g.Where(x => x.IdLoaiPhieu == MTLEnum.LoaiPhieu.HoaDon).Sum(x => DoanhThuSoTien(x, isTienThucCo));
					var dv = g.Where(x => x.IdLoaiPhieu == MTLEnum.LoaiPhieu.DichVu).Sum(x => DoanhThuSoTien(x, isTienThucCo));
					var d = g.Key;
					return new MTL_ThongKeDoanhThuResult
					{
						ngaytrongthang_date = d,
						ngay = d.Day,
						ngaytrongthang = d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
						doanhthu = hh,
						doanhthudichvu = dv,
						doanhthuhanghoa = hh
					};
				})
				.ToList();
		}
		/// <summary>Tổng tiền mặt / CK đã thu / CK dự kiến trong khoảng ngày (theo ngày trả phiếu), loại trừ báo giá, chỉ tính hóa đơn ĐÃ XỦ LÝ.</summary>
		public async Task<ThuTheoNguonThoiGianSummary> GetThuTheoNguonTrongKhoangAsync(int idcompany, DateTime tuNgay, DateTime denNgay)
		{
			if (idcompany <= 0)
				return new ThuTheoNguonThoiGianSummary();
			var tu = tuNgay.Date;
			var den = denNgay.Date.AddDays(1).AddTicks(-1);
			var rows = await _context.TblHoaDonBanHangs.AsNoTracking()
				.Where(h => h.Idcompany == idcompany
					&& h.Ngaytraphieu >= tu && h.Ngaytraphieu <= den
					&& h.IsDichVuDaXuLy == true
					&& (h.IdLoaiPhieu == null || h.IdLoaiPhieu != MTLEnum.LoaiPhieu.BaoGia))
				.Select(h => new
				{
					Tm = h.Tienmat ?? 0,
					Ck = h.Tienchuyenkhoan ?? 0,
					Dk = h.Tienchuyenkhoandukien ?? 0
				})
				.ToListAsync();
			return new ThuTheoNguonThoiGianSummary
			{
				TienMat = rows.Sum(x => x.Tm),
				TienChuyenKhoan = rows.Sum(x => x.Ck),
				TienChuyenKhoanDuKien = rows.Sum(x => x.Dk)
			};
		}

		public Task<ThuTheoNguonThoiGianSummary> GetThuTheoNguonTrongThangAsync(int idcompany, int nam, int thang)
		{
			var tu = new DateTime(nam, thang, 1);
			var den = tu.AddMonths(1).AddDays(-1);
			return GetThuTheoNguonTrongKhoangAsync(idcompany, tu, den);
		}

		/// <summary>
		/// Nguồn tiền trong tháng: tiền mặt + (CK đã thu + CK/trả góp dự kiến <c>tienchuyenkhoandukien</c>) theo từng TK;
		/// phần CK có số nhưng chưa gán <c>Idbank</c> gộp lát <c>ck_unassigned</c>.
		/// Trả góp: phần còn lại lưu ở <c>Tienchuyenkhoandukien</c> + <c>Idbank</c> — phải cộng vào đúng TK (không chỉ <c>Tienchuyenkhoan</c>).
		/// Chỉ hóa đơn ĐÃ XỬ LÝ (IsDichVuDaXuLy == true).
		/// </summary>
		public async Task<ThuDaVaoTheoNguonResult> GetThuDaVaoTheoNguonTrongThangAsync(int idcompany, int nam, int thang)
		{
			if (idcompany <= 0)
				return new ThuDaVaoTheoNguonResult();

			var tu = new DateTime(nam, thang, 1).Date;
			var den = tu.AddMonths(1).AddTicks(-1);

			var baseQ = _context.TblHoaDonBanHangs.AsNoTracking()
				.Where(h => h.Idcompany == idcompany
					&& h.Ngaytraphieu >= tu && h.Ngaytraphieu <= den
					&& h.IsDichVuDaXuLy == true
					&& (h.IdLoaiPhieu == null || h.IdLoaiPhieu != MTLEnum.LoaiPhieu.BaoGia));

			var tienMat = await baseQ.SumAsync(h => h.Tienmat ?? 0);

			var paymentRows = await baseQ
				.Select(h => new { h.Idbank, Ck = h.Tienchuyenkhoan ?? 0, Dk = h.Tienchuyenkhoandukien ?? 0 })
				.ToListAsync();

			var ckUnassigned = paymentRows
				.Where(h => h.Idbank == null)
				.Sum(h => h.Ck + h.Dk);

			var ckByBank = paymentRows
				.Where(h => h.Idbank != null)
				.GroupBy(h => h.Idbank!.Value)
				.Select(g => new { BankId = g.Key, Sum = g.Sum(x => x.Ck + x.Dk) })
				.Where(x => x.Sum >= 0.01)
				.ToList();

			var banks = await _context.TblBanks.AsNoTracking()
				.Where(b => b.Idcompany == idcompany)
				.OrderBy(b => b.Id)
				.ToListAsync();
			var bankLabel = banks.ToDictionary(
				b => b.Id,
				b => $"{b.BankingName} {b.AccountName}".Trim());

			var slices = new List<ThuDaVaoNguonSlice>();
			if (tienMat >= 0.01)
				slices.Add(new ThuDaVaoNguonSlice
				{
					Key = "cash",
					Label = "Tiền mặt",
					Amount = tienMat
				});

			var bankIdsOrdered = banks.Select(b => b.Id).ToList();
			var seenBankIds = new HashSet<int>();

			foreach (var bid in bankIdsOrdered)
			{
				var row = ckByBank.FirstOrDefault(x => x.BankId == bid);
				var sum = row?.Sum ?? 0;
				if (sum < 0.01) continue;
				seenBankIds.Add(bid);
				var label = bankLabel.TryGetValue(bid, out var lb) && !string.IsNullOrWhiteSpace(lb)
					? lb
					: $"TK #{bid}";
				slices.Add(new ThuDaVaoNguonSlice
				{
					Key = $"bank:{bid}",
					Label = label,
					Amount = sum
				});
			}

			foreach (var row in ckByBank.OrderBy(x => x.BankId))
			{
				if (seenBankIds.Contains(row.BankId) || row.Sum < 0.01) continue;
				var label = bankLabel.TryGetValue(row.BankId, out var lb) && !string.IsNullOrWhiteSpace(lb)
					? lb
					: $"TK #{row.BankId} (không trong cấu hình hiện tại)";
				slices.Add(new ThuDaVaoNguonSlice
				{
					Key = $"bank:{row.BankId}",
					Label = label,
					Amount = row.Sum
				});
			}

			if (ckUnassigned >= 0.01)
				slices.Add(new ThuDaVaoNguonSlice
				{
					Key = "ck_unassigned",
					Label = "CK / thẻ (chưa gán TK)",
					Amount = ckUnassigned
				});

			return new ThuDaVaoTheoNguonResult { Slices = slices };
		}

		/// <summary>
		/// Nguồn tiền đã vào từng ngày trong tháng; thứ tự cột trùng <see cref="GetThuDaVaoTheoNguonTrongThangAsync"/> (chỉ slice có tổng tháng ≥ 0,01).
		/// </summary>
		/// <param name="thangDaThuTheoNguon">Nếu đã có kết quả tháng (vừa gọi <see cref="GetThuDaVaoTheoNguonTrongThangAsync"/>), truyền vào để tránh truy vấn lặp.</param>
		public async Task<ThuDaVaoTheoNguonTheoNgayResult> GetThuDaVaoTheoNguonTheoNgayTrongThangAsync(int idcompany, int nam, int thang, ThuDaVaoTheoNguonResult? thangDaThuTheoNguon = null)
		{
			if (idcompany <= 0)
				return new ThuDaVaoTheoNguonTheoNgayResult();

			var monthAgg = thangDaThuTheoNguon ?? await GetThuDaVaoTheoNguonTrongThangAsync(idcompany, nam, thang);
			var slices = monthAgg.Slices.Where(s => s.Amount >= 0.01).ToList();
			if (slices.Count == 0)
				return new ThuDaVaoTheoNguonTheoNgayResult();

			var tu = new DateTime(nam, thang, 1).Date;
			var den = tu.AddMonths(1).AddTicks(-1);

			var flat = await _context.TblHoaDonBanHangs.AsNoTracking()
				.Where(h => h.Idcompany == idcompany
					&& h.Ngaytraphieu != null
					&& h.Ngaytraphieu >= tu && h.Ngaytraphieu <= den
					&& h.IsDichVuDaXuLy == true
					&& (h.IdLoaiPhieu == null || h.IdLoaiPhieu != MTLEnum.LoaiPhieu.BaoGia))
				.Select(h => new
				{
					Day = h.Ngaytraphieu!.Value.Date,
					Tm = h.Tienmat ?? 0,
					Ck = h.Tienchuyenkhoan ?? 0,
					Dk = h.Tienchuyenkhoandukien ?? 0,
					h.Idbank
				})
				.ToListAsync();

			var byDay = new Dictionary<DateTime, Dictionary<string, double>>();
			foreach (var r in flat)
			{
				if (!byDay.TryGetValue(r.Day, out var dict))
				{
					dict = new Dictionary<string, double>(StringComparer.Ordinal);
					byDay[r.Day] = dict;
				}
				if (r.Tm >= 0.01)
					dict["cash"] = dict.GetValueOrDefault("cash") + r.Tm;
				var bankTotal = r.Ck + r.Dk;
				if (bankTotal >= 0.01)
				{
					if (r.Idbank == null)
						dict["ck_unassigned"] = dict.GetValueOrDefault("ck_unassigned") + bankTotal;
					else
					{
						var k = $"bank:{r.Idbank.Value}";
						dict[k] = dict.GetValueOrDefault(k) + bankTotal;
					}
				}
			}

			var keys = slices.Select(s => s.Key).ToList();
			var labels = slices.Select(s => s.Label).ToList();
			var rows = new List<ThuDaVaoTheoNguonNgayRow>();
			foreach (var kv in byDay.OrderBy(x => x.Key))
			{
				var dict = kv.Value;
				var amounts = keys.Select(k => dict.GetValueOrDefault(k)).ToList();
				if (amounts.Sum() < 0.01)
					continue;
				rows.Add(new ThuDaVaoTheoNguonNgayRow
				{
					Ngay = kv.Key,
					AmountsBySlice = amounts
				});
			}

			return new ThuDaVaoTheoNguonTheoNgayResult
			{
				SourceKeys = keys,
				SourceLabels = labels,
				Rows = rows
			};
		}

		/// <summary>Nhập hàng theo ngày trong tháng: tổng nhập từng ngày (từ bảng ThuHangHoa, cùng nguồn với DSThuHangHoa).</summary>
		public async Task<NhapHangTheoNgayResult> GetNhapHangTheoNgayTrongThangAsync(int idcompany, int nam, int thang)
		{
			if (idcompany <= 0)
				return new NhapHangTheoNgayResult();

			var tu = new DateTime(nam, thang, 1).Date;
			var den = tu.AddMonths(1).AddTicks(-1);

			// Query trực tiếp bảng ThuHangHoa giống DSThuHangHoa
			var flat = await _context.TblThuHangHoas.AsNoTracking()
				.Where(h => h.Idcompany == idcompany
					&& h.Ngaythu != null
					&& h.Ngaythu >= tu && h.Ngaythu <= den)
				.Select(h => new
				{
					Day = h.Ngaythu!.Value.Date,
					Tong = h.Thanhtoan ?? 0
				})
				.ToListAsync();

			var byDay = flat
				.GroupBy(r => r.Day)
				.Select(g => new NhapHangTheoNgayRow
				{
					Ngay = g.Key,
					TongNgay = g.Sum(x => x.Tong)
				})
				.OrderBy(r => r.Ngay)
				.ToList();

			return new NhapHangTheoNgayResult { Rows = byDay };
		}

		/// <summary>
		/// KPI tổng quan (hôm nay, tuần, tháng…): cùng điều kiện lọc với <see cref="GetDoanhThuAsync"/>.
		/// Tuần: từ thứ Hai đến hết ngày mốc; tuần trước = tuần Monday–Sunday trước tuần hiện tại.
		/// </summary>
		public async Task<List<MTL_ThongKeDoanhThuCoBanResult>> GetDoanhThuCoBanAsync(int idcompany, string NgayHienTai, bool isTienThucCo)
		{
			if (idcompany <= 0)
				return new List<MTL_ThongKeDoanhThuCoBanResult>();

			if (!DateTime.TryParse(NgayHienTai, CultureInfo.InvariantCulture, DateTimeStyles.None, out var refDay))
				refDay = DateTime.Today;
			refDay = refDay.Date;

			var startToday = refDay;
			var endToday = refDay.AddDays(1).AddTicks(-1);
			var startYesterday = refDay.AddDays(-1);
			var endYesterday = refDay.AddTicks(-1);

			var startThisWeek = StartOfWeekMonday(refDay);
			var endThisWeek = endToday;
			var startLastWeek = startThisWeek.AddDays(-7);
			var endLastWeek = startThisWeek.AddTicks(-1);

			var startThisMonth = new DateTime(refDay.Year, refDay.Month, 1);
			var endThisMonthMtd = endToday;
			var startPrevMonth = startThisMonth.AddMonths(-1);
			var endPrevMonth = startThisMonth.AddTicks(-1);

			var minDate = startPrevMonth < startLastWeek ? startPrevMonth : startLastWeek;
			var allInRange = await DoanhThuBaseQuery(idcompany)
				.Where(h => h.Ngaytraphieu >= minDate && h.Ngaytraphieu <= endToday)
				.ToListAsync();

			double SumRange(DateTime a, DateTime b) =>
				allInRange
					.Where(h => h.Ngaytraphieu >= a && h.Ngaytraphieu <= b)
					.Sum(h => DoanhThuSoTien(h, isTienThucCo));

			var homnay = SumRange(startToday, endToday);
			var homqua = SumRange(startYesterday, endYesterday);
			var tuannay = SumRange(startThisWeek, endThisWeek);
			var tuantruoc = SumRange(startLastWeek, endLastWeek);
			var thangnay = SumRange(startThisMonth, endThisMonthMtd);
			var thangtruoc = SumRange(startPrevMonth, endPrevMonth);

			var one = new MTL_ThongKeDoanhThuCoBanResult
			{
				ngayhomnay = refDay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
				ngayhomqua = refDay.AddDays(-1).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
				homnay = homnay,
				homqua = homqua,
				tuannay = tuannay,
				tuantruoc = tuantruoc,
				thangnay = thangnay,
				thangtruoc = thangtruoc
			};

			return new List<MTL_ThongKeDoanhThuCoBanResult> { one };
		}

		static DateTime StartOfWeekMonday(DateTime date)
		{
			var d = date.Date;
			var diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
			return d.AddDays(-diff);
		}

		/// <summary>Chỉ Hóa đơn + Dịch vụ; đã xử lý; không báo giá.</summary>
		IQueryable<TblHoaDonBanHang> DoanhThuBaseQuery(int idcompany) =>
			_context.TblHoaDonBanHangs.AsNoTracking()
				.Where(h => h.Idcompany == idcompany
					&& h.Ngaytraphieu != null
					&& h.IsDichVuDaXuLy == true
					&& (h.IdLoaiPhieu == MTLEnum.LoaiPhieu.HoaDon || h.IdLoaiPhieu == MTLEnum.LoaiPhieu.DichVu)
					&& (h.IsBaoGiaKoTinhDoanhThu == null || h.IsBaoGiaKoTinhDoanhThu == false));

		/// <param name="isTienThucCo">
		/// true: TM + CK đã thu + CK/trả góp dự kiến (cùng ngày ghi nhận đủ doanh thu với trả góp).
		/// false: <see cref="TblHoaDonBanHang.Tongtiensauvat"/> (tổng sau VAT trên phiếu).
		/// </param>
		static double DoanhThuSoTien(TblHoaDonBanHang h, bool isTienThucCo)
		{
			if (isTienThucCo)
				return (h.Tienmat ?? 0) + (h.Tienchuyenkhoan ?? 0) + (h.Tienchuyenkhoandukien ?? 0);
			return h.Tongtiensauvat ?? 0;
		}

		/// <summary>
		/// Top N sản phẩm bán chạy nhất trong năm, theo SL hoặc doanh thu.
		/// Chỉ lấy dòng có <c>Idsanpham != null</c> (loại bỏ dịch vụ thuần túy không gán sản phẩm).
		/// Chỉ Hóa đơn (IdLoaiPhieu == HoaDon) — không lấy DichVu để tránh trùng dịch vụ.
		/// Cùng bộ lọc Đã xử lý / không báo giá với GetDoanhThuAsync.
		/// </summary>
		/// <param name="idcompany">ID công ty.</param>
		/// <param name="nam">Năm cần xem.</param>
		/// <param name="topN">Số lượng top cần lấy.</param>
		/// <param name="sortBy">Sắp xếp theo SL hay Doanh thu.</param>
		public async Task<List<TopSanPhamRow>> GetTopSanPhamBanChayAsync(int idcompany, int nam, int topN = 10, TopSanPhamSortBy sortBy = TopSanPhamSortBy.SoLuong)
		{
			if (idcompany <= 0 || nam < 1 || topN < 1)
				return new List<TopSanPhamRow>();

			var tu = new DateTime(nam, 1, 1).Date;
			var den = new DateTime(nam, 12, 31).AddDays(1).AddTicks(-1);

			// Chỉ Hóa đơn (IdLoaiPhieu == HoaDon) — không lấy DichVu để tránh trùng dịch vụ
			var validMahds = _context.TblHoaDonBanHangs.AsNoTracking()
				.Where(h => h.Idcompany == idcompany
					&& h.Ngaytraphieu != null
					&& h.Ngaytraphieu >= tu && h.Ngaytraphieu <= den
					&& h.IsDichVuDaXuLy == true
					&& h.IdLoaiPhieu == MTLEnum.LoaiPhieu.HoaDon
					&& (h.IsBaoGiaKoTinhDoanhThu == null || h.IsBaoGiaKoTinhDoanhThu == false))
				.Select(h => h.Mahd!)
				.ToList();

			if (validMahds.Count == 0)
				return new List<TopSanPhamRow>();

			// Lấy Mã SP (Masp) từ bảng TblSanpham
			var maspById = _context.TblSanphams.AsNoTracking()
				.Where(s => s.Idcompany == idcompany)
				.ToDictionary(s => s.Id, s => s.Masp ?? "");

			// Lấy danh sách Mahd thuộc Hóa đơn (IdLoaiPhieu == HoaDon) — loại DichVu
			var hoaDonMahds = _context.TblHoaDonBanHangs.AsNoTracking()
				.Where(h => h.Idcompany == idcompany
					&& h.Ngaytraphieu != null
					&& h.Ngaytraphieu >= tu && h.Ngaytraphieu <= den
					&& h.IsDichVuDaXuLy == true
					&& h.IdLoaiPhieu == MTLEnum.LoaiPhieu.HoaDon
					&& (h.IsBaoGiaKoTinhDoanhThu == null || h.IsBaoGiaKoTinhDoanhThu == false))
				.Select(h => h.Mahd!)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			// Chi tiết — chỉ dòng có Idsanpham != null (không phải dịch vụ thuần), SL > 0
			// Đồng thời lọc bỏ: sản phẩm ảo (issanphamao == true) và dòng thuộc DichVu (lọc qua Mahd)
			var raw = await Task.Run(() =>
				_context.TblHoaDonBanHangChiTiets.AsNoTracking()
					.Where(c => c.Idsanpham != null
						&& validMahds.Contains(c.Mahd ?? "")
						&& (c.Sl ?? 0) > 0
						&& (c.Issanphamao == null || c.Issanphamao == false)
						&& hoaDonMahds.Contains(c.Mahd ?? ""))
					.ToList());

			// Nhóm theo Idsanpham (đã lọc != null ở trên)
			var grouped = raw
				.GroupBy(c => c.Idsanpham!.Value)
				.Select(g => new
				{
					Idsanpham = g.Key,
					Tensp = g.First().Tensp ?? "",
					TongSl = g.Sum(c => c.Sl ?? 0),
					TongDt = g.Sum(c => c.Thanhtien ?? 0),
					SoDon = g.Select(c => c.Mahd).Distinct().Count()
				})
				.Where(x => x.TongSl >= 0.01 || x.TongDt >= 0.01)
				.OrderByDescending(x => sortBy == TopSanPhamSortBy.DoanhThu ? x.TongDt : x.TongSl)
				.Take(topN)
				.ToList();

			return grouped.Select(g => new TopSanPhamRow
			{
				Idsanpham = g.Idsanpham,
				Tensp = g.Tensp,
				Masp = maspById.TryGetValue(g.Idsanpham, out var m) ? m : "",
				TongSl = g.TongSl,
				TongDoanhThu = g.TongDt,
				GiaTb = g.TongSl > 0 ? g.TongDt / g.TongSl : 0,
				SoDon = g.SoDon
			}).ToList();
		}

	}
}
