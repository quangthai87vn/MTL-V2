using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.APP.Models;
using MTL.APP.Utils;

namespace MTL.APP.Services
{
	public class InvoiceService : ControllerBase
	{
		private readonly IDbContextFactory<MTLContext> _contextFactory;
		public InvoiceService(IDbContextFactory<MTLContext> contextFactory)
		{
			_contextFactory = contextFactory;
		}
		public async Task<TblHoaDonBanHang> MTL_GetInvoiceByMaHDAsync(string MaHD)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			TblHoaDonBanHang data;
			data = await context.TblHoaDonBanHangs.FindAsync(MaHD);
			return data;
		}

		public async Task<List<MTL_GetAllInvoiceResult>> GetAllInvoiceAsync(int idcompany, string OrderBy, int idLoaiPhieu, int tinhtrangdichvu, bool isLocNgay, string TuNgay, string DenNgay, bool isHienCongNo, List<int> ListIDTags)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			IQueryable<MTL_GetAllInvoiceResult> invoice;

			invoice = context.MTL_GetAllInvoice(idcompany);

			// Lọc Loại Phiếu
			if (idLoaiPhieu != 1)// ko phãi tất cả mới lọc
			{
				invoice = invoice.Where(b => b.idLoaiPhieu == idLoaiPhieu);
			}
			// Lọc tinhtrangdichvu
			if (idLoaiPhieu != 0)// ko phãi tất cả mới lọc
			{
				if (tinhtrangdichvu == 1)// đã xữ lý
					invoice = invoice.Where(b => b.isDichVuDaXuLy == false);
				else if (tinhtrangdichvu == 2)// chưa xữ lý
					invoice = invoice.Where(b => b.isDichVuDaXuLy == true);
			}
			//Loc theo ngay chon
			if (isLocNgay)
			{
				CultureInfo provider = new CultureInfo("en-CA");
				DateTime c_tungay = DateTime.ParseExact(TuNgay, "MM/dd/yyyy", provider);
				DateTime c_denngay = DateTime.ParseExact(DenNgay, "MM/dd/yyyy", provider);
				c_denngay = c_denngay.AddHours(23).AddMinutes(59).AddSeconds(59);
				invoice = invoice.Where(b => b.ngaylapphieu >= c_tungay
								&& b.ngaylapphieu <= c_denngay);
			}
			if (isHienCongNo)
			{
				invoice = invoice.Where(b => b.conlai > 0);
			}


			if (OrderBy == "Ngày bán") invoice = invoice.OrderByDescending(b => b.ngaylapphieu);
			else if (OrderBy == "Ngày cập nhật") invoice = invoice.OrderByDescending(b => b.lastupdate);

			if (ListIDTags.Count > 0)
			{
				int[] LTags = ListIDTags.ToArray();
				invoice = invoice.Join(context.TblHoaDonBanHangTagMaps, iv => iv.mahd, tm => tm.Mahd
										, (iv, tm) => new { iv, tm })
								  .Where(x => LTags.Contains((int)(x.tm.Tagid)))
								  .Select(c => c.iv);
			}
			return await invoice.ToListAsync();
		}
		public async Task<List<uf_GetAllPhieuDVResult>> GetAllServices(string Where_XuLy)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			IQueryable<uf_GetAllPhieuDVResult>? invoice;
			invoice = context.uf_GetAllPhieuDV();
			if (Where_XuLy == "Đang xử lý") invoice = invoice.Where(b => b.xuly == false);
			else if (Where_XuLy == "Đã xử lý") invoice = invoice.Where(b => b.xuly == true);
			return await invoice.ToListAsync();
		}
		public async Task<List<MTL_GetHoaDonChiTietResult>> GetHoaDonChiTiet_MaHDAsync(string MaHD)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			IQueryable<MTL_GetHoaDonChiTietResult>? invoice_detail;
			invoice_detail = context.MTL_GetHoaDonChiTiet(MaHD);
			return await invoice_detail.ToListAsync();
		}

		public async Task<List<MTL_GetHoaDonResult>> GetHoaDon(string MaHD)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			IQueryable<MTL_GetHoaDonResult>? invoice;
			invoice = context.MTL_GetHoaDon(MaHD);
			return await invoice.ToListAsync();
		}

		public async Task<List<MTL_GetHoaDonBySDT_SerialResult>> GetHoaDon(int idcompany, string SDT, string Serial)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			IQueryable<MTL_GetHoaDonBySDT_SerialResult>? invoice;
			invoice = context.MTL_GetHoaDonBySDT_Serial(idcompany, SDT, Serial);
			return await invoice.ToListAsync();
		}



		public async Task<string> CreateMaHD(string MaTuDong)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			string MaHD = "";
			MaHD = context.Database
				.SqlQueryRaw<string>(string.Format("select [dbo].[CreateMaHD]('{0}')  as 'Value'", MaTuDong))
				.FirstOrDefault().Replace(" ", string.Empty);
			return MaHD;
		}

		public async Task<TblHoaDonBanHang> DeleteInvoice(TblHoaDonBanHang hdbh)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			var _hdbhct = from r in context.TblHoaDonBanHangChiTiets
						  where (r.Mahd == hdbh.Mahd)
						  select r;
			if (_hdbhct != null)
			{
				context.TblHoaDonBanHangChiTiets.RemoveRange(_hdbhct);
				await context.SaveChangesAsync();
			}
			TblHoaDonBanHang _hdbh = await context.TblHoaDonBanHangs.FindAsync(hdbh.Mahd);
			if (_hdbh != null)
			{
				context.TblHoaDonBanHangs.Remove(_hdbh);
				await context.SaveChangesAsync();
			}
			return _hdbh;
		}

		public async Task<TblHoaDonBanHang> DeleteProductToInvoiceAsync(HoaDonBanHangResult hdbh)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			TblHoaDonBanHang _hdbh = hdbh.HoaDonBanHang;
			TblHoaDonBanHangChiTiet _hdbhct = hdbh.HoaDonBanHangChiTiet;
			var sanpham = await context.TblHoaDonBanHangChiTiets.FindAsync(_hdbhct.Id);
			if (sanpham != null)
			{
				context.TblHoaDonBanHangChiTiets.Remove(sanpham);
				await context.SaveChangesAsync();
			}
			_hdbh = await UpdateHoaDonBanHangAsync(_hdbh, context);
			return _hdbh;
		}

		public async Task<TblHoaDonBanHang> AddProductToInvoiceAsync(HoaDonBanHangResult hdbh, int idcompany)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			TblHoaDonBanHang _hdbh = hdbh.HoaDonBanHang;
			TblHoaDonBanHangChiTiet _hdbhct = hdbh.HoaDonBanHangChiTiet;
			bool HoaDonExists;
			int result_hd = 0;
			HoaDonExists = context.TblHoaDonBanHangs.Any(hd => hd.Mahd == _hdbh.Mahd);
			if (HoaDonExists)
			{
				// đã tồn tại Hóa đơn
			}
			else
			{
				// chưa tồn tại hóa đơn, thì thêm mới
				await context.TblHoaDonBanHangs.AddAsync(_hdbh);
				result_hd = await context.SaveChangesAsync();
			}
			//insert bang Hoa don ban hang chi tiet
			int result = 0;
			//CHECK CÓ PHẢI SP ẢO HAY KO ?
			if (_hdbhct.Issanphamao == true)
			{
				// là ảo
				await context.TblHoaDonBanHangChiTiets.AddAsync(_hdbhct);
				result = await context.SaveChangesAsync();
			}
			else
			{
				//ko là sp ảo
				//insert bang Hoa don ban hang chi tiet, KIỂM TRA idsanpham thuộc hàng hóa thành phần hay ko ?           
				var sanpham = await context.TblSanphams.FindAsync(_hdbhct.Idsanpham);

				if (sanpham.Idloaihanghoadichvu == 3)
				{
					int kq = await context.Procedures.sp_SaveHangHoaThanhPhanToHoaDonAsync(sanpham.Id, _hdbh.Mahd, idcompany);
					result = await context.SaveChangesAsync();
				}
				else
				{
					// ko phải Hàng hoá thành phần thì insert 1 dòng
					await context.TblHoaDonBanHangChiTiets.AddAsync(_hdbhct);
					result = await context.SaveChangesAsync();
				}
			}
			// kiểm tra Post san pham thanh cong
			//if (result > 0)
			//{
			//update tong tien HD
			_hdbh = await UpdateHoaDonBanHangAsync(_hdbh, context);
			//}           
			return _hdbh;
		}

		public async Task<TblHoaDonBanHang> UpdateHoaDonBanHangAsync(TblHoaDonBanHang _hdbh, MTLContext context)
		{
			//update lại tổng tiền trên HD
			TblHoaDonBanHang hoadon = new TblHoaDonBanHang();
			var items_hd = (from o in context.TblHoaDonBanHangChiTiets
							where o.Mahd == _hdbh.Mahd
							select new { o.Mahd, o.Thanhtien }
							).ToList();
			var sum = items_hd.Select(c => c.Thanhtien).Sum();
			hoadon = context.TblHoaDonBanHangs.Single(a => a.Mahd == _hdbh.Mahd);
			hoadon.Hoadonvat = _hdbh.Hoadonvat;
			hoadon.Idkhachhang = _hdbh.Idkhachhang;
			hoadon.Ghichu = _hdbh.Ghichu;
			hoadon.Ghichukohienbill = _hdbh.Ghichukohienbill;
			hoadon.Lastupdate = DateTime.Now;
			hoadon.Ngaylapphieu = _hdbh.Ngaylapphieu;
			hoadon.Idnguoixuat = _hdbh.Idnguoixuat;
			hoadon.Tienck = _hdbh.Tienck;
			hoadon.Vat = _hdbh.Vat;
			hoadon.IsVat = _hdbh.IsVat;
			hoadon.Tongtien = sum;
			double tongtien = Convert.ToDouble(hoadon.Tongtien);
			double tienck = Convert.ToDouble(hoadon.Tienck);
			hoadon.Tienck = tienck;
			hoadon.Giamgia = (tongtien - tienck);
			hoadon.Tongtiensauvat = hoadon.Giamgia;
			// Giữ đúng hình thức TT + tách tiền theo tổng mới (trước đây ép 100% TM làm lệch Trả góp / TM+CK).
			ApplyPaymentSplitAfterLineItemsChange(hoadon, _hdbh);
			hoadon.IdLoaiPhieu = _hdbh.IdLoaiPhieu;
			hoadon.Iduserlastupdate = _hdbh.Iduserlastupdate;
			hoadon.Serial = _hdbh.Serial;
			hoadon.Model = _hdbh.Model;
			//if (_hdbh.IdLoaiPhieu == 3)//là dịch vụ mới lưu
			//{
			hoadon.Ngaytraphieu = _hdbh.Ngaytraphieu;
			hoadon.IsDichVuDaXuLy = _hdbh.IsDichVuDaXuLy;
			//}
			int result = await context.SaveChangesAsync();
			return hoadon;
		}
		public async Task<TblHoaDonBanHang> UpdateHoaDonBanHangFinishAsync(TblHoaDonBanHang _hdbh, List<MTL.APP.Models.Tag> tags)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			//update lại tổng tiền trên HD
			TblHoaDonBanHang hoadon = new TblHoaDonBanHang();
			var items_hd = (from o in context.TblHoaDonBanHangChiTiets
							where o.Mahd == _hdbh.Mahd
							select new { o.Mahd, o.Thanhtien }
							).ToList();
			var sum = items_hd.Select(c => c.Thanhtien).Sum();
			hoadon = context.TblHoaDonBanHangs.Single(a => a.Mahd == _hdbh.Mahd);
			hoadon.Hoadonvat = _hdbh.Hoadonvat;
			hoadon.Idkhachhang = _hdbh.Idkhachhang;
			hoadon.Ghichu = _hdbh.Ghichu;
			hoadon.Ghichukohienbill = _hdbh.Ghichukohienbill;
			hoadon.Lastupdate = DateTime.Now;
			hoadon.Ngaylapphieu = _hdbh.Ngaylapphieu;
			hoadon.Idnguoixuat = _hdbh.Idnguoixuat;
			hoadon.Tienck = _hdbh.Tienck;
			hoadon.Vat = _hdbh.Vat;
			hoadon.IsVat = _hdbh.IsVat;
			hoadon.Tongtien = sum;
			double tongtien = Convert.ToDouble(hoadon.Tongtien);
			double tienck = Convert.ToDouble(hoadon.Tienck);
			hoadon.Tienck = tienck;
			hoadon.Giamgia = (tongtien - tienck);
			hoadon.Tongtiensauvat = _hdbh.Tongtiensauvat; //hoadon.Giamgia;
			hoadon.Thanhtoan = _hdbh.Thanhtoan;// hoadon.Tongtiensauvat;
			hoadon.Conlai = _hdbh.Conlai;//hoadon.Tongtiensauvat - hoadon.Thanhtoan;
			hoadon.IdLoaiPhieu = _hdbh.IdLoaiPhieu;
			hoadon.Iduserlastupdate = _hdbh.Iduserlastupdate;
			hoadon.Serial = _hdbh.Serial;
			hoadon.Model = _hdbh.Model;
			hoadon.IsDichVuDaXuLy = _hdbh.IsDichVuDaXuLy;
			hoadon.Idhinhthucthanhtoan = _hdbh.Idhinhthucthanhtoan;
			hoadon.Tienmat = _hdbh.Tienmat;
			hoadon.Tienchuyenkhoan = _hdbh.Tienchuyenkhoan;
			hoadon.Tienchuyenkhoandukien = _hdbh.Tienchuyenkhoandukien;
			hoadon.Ngaydukienchuyenkhoan = _hdbh.Ngaydukienchuyenkhoan;
			var tienCkDaThu = _hdbh.Tienchuyenkhoan ?? 0;
			var tienCkDuKien = _hdbh.Tienchuyenkhoandukien ?? 0;
			var htt = _hdbh.Idhinhthucthanhtoan ?? 0;
			bool canBank = tienCkDaThu > 0.009 || tienCkDuKien > 0.009 || htt == 2 || htt == 3 || htt == 4 || htt == 5;
			if (canBank)
				hoadon.Idbank = _hdbh.Idbank;
			else
				hoadon.Idbank = null;
            //if (_hdbh.IdLoaiPhieu == 3)//là dịch vụ mới lưu
            //{
            hoadon.Ngaytraphieu = _hdbh.Ngaytraphieu;
			hoadon.IsDichVuDaXuLy = _hdbh.IsDichVuDaXuLy;
			//}
			int result = await context.SaveChangesAsync();
			// Lưu Tags
			if (tags != null)
			{
				await SaveTagHoaDonAsync(hoadon, tags, context);
			}

			return hoadon;
		}
		public async Task<List<MTL.APP.Models.Tag>> SaveTagHoaDonAsync(TblHoaDonBanHang hoadon, List<MTL.APP.Models.Tag> tags, MTLContext context)
		{
			// Xóa tất cả Tag theo ID Hoa don
			List<MTL.APP.Models.Tag> listTags = new List<Tag>();
			var _tblHoaDonBanHangTagMaps = from r in context.TblHoaDonBanHangTagMaps
										   where (r.Mahd == hoadon.Mahd)
										   select r;
			if (_tblHoaDonBanHangTagMaps != null)
			{
				context.TblHoaDonBanHangTagMaps.RemoveRange(_tblHoaDonBanHangTagMaps);
				await context.SaveChangesAsync();
			}
			// Insert lại Tag	
			foreach (MTL.APP.Models.Tag tag in tags)
			{
				TblHoaDonBanHangTagMap tm = new TblHoaDonBanHangTagMap();
				tm.Tagid = tag.Id;
				tm.Mahd = hoadon.Mahd;
				await context.TblHoaDonBanHangTagMaps.AddAsync(tm);
				await context.SaveChangesAsync();
			}
			return listTags;
		}
		public async Task<List<LoaiPhieu>> GetLoaiPhieuAsync()
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			return await context.LoaiPhieus
				// .Where(b => b.Id != 4) // Loại Tất cả ra
				.ToListAsync();
		}
		public async Task<TblHoaDonBanHang> UpdateProDuctToHoaDonChiTietAsync(TblHoaDonBanHangChiTiet hdbhct)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			TblHoaDonBanHangChiTiet _hdbhct = context.TblHoaDonBanHangChiTiets.Single(a => a.Id == hdbhct.Id);
			_hdbhct.Dongia = hdbhct.Dongia;
			_hdbhct.Idsanpham = hdbhct.Idsanpham;
			_hdbhct.Issanphamao = hdbhct.Issanphamao;
			_hdbhct.Lastupdate = DateTime.Now;
			_hdbhct.Sl = hdbhct.Sl;
			_hdbhct.Tensp = hdbhct.Tensp;
			_hdbhct.Thanhtien = hdbhct.Sl * hdbhct.Dongia;
			_hdbhct.Idbaohanh = hdbhct.Idbaohanh;
			_hdbhct.Mahd = hdbhct.Mahd;
			_hdbhct.Id = hdbhct.Id;
			_hdbhct.Idhoadon = hdbhct.Idhoadon;
			int result = await context.SaveChangesAsync();
			// kiểm tra Update san pham thanh cong
			//if (result > 0)
			//{
			//update tong tien HD
			TblHoaDonBanHang hdbh = context.TblHoaDonBanHangs.Single(a => a.Mahd == hdbhct.Mahd);
			hdbh = await UpdateHoaDonBanHangAsync(hdbh, context);
			//}         
			return hdbh;
		}

		public async Task<TblHoaDonBanHang> CopyInvoiceAsync(TblHoaDonBanHang hdbh)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			int result = 0;
			var mahd_old = hdbh.Mahd;
			var mahd_new = "";
			TblHoaDonBanHang _hdbh = await context.TblHoaDonBanHangs.FindAsync(mahd_old);
			int kq = await context.Procedures.sp_CopyHoaDonAsync(mahd_old);
			result = await context.SaveChangesAsync();
			return _hdbh;
		}

		//VietNhatTapHoa
		public async Task<string> CVJP_CreateMaPhieuDatHangAsync(int idnhanviendathang)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			string MaHD = "";
			MaHD = context.Database
				.SqlQueryRaw<string>(string.Format("select [dbo].[CVJP_CreateMaPhieuDatHang]('{0}')  as 'Value'", idnhanviendathang))
				.FirstOrDefault().Replace(" ", string.Empty);
			return MaHD;
		}
		public async Task<CvjpDonHang> CVJP_ThemSPVaoPhieuDatHangAsync(DatHangResult dathang)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			CvjpDonHang donhang = dathang.CVJP_DonHang;
			CvjpDonHangChiTiet donhangchitiet = dathang.CVJP_DonHangChiTiet;
			bool HoaDonExists;
			int result_hd = 0;
			HoaDonExists = context.CvjpDonHangs.Any(hd => hd.Mahd == donhang.Mahd);
			if (HoaDonExists)
			{
				// đã tồn tại Hóa đơn
			}
			else
			{
				// chưa tồn tại hóa đơn, thì thêm mới
				await context.CvjpDonHangs.AddAsync(donhang);
				result_hd = await context.SaveChangesAsync();
			}
			//insert bang Hoa don ban hang chi tiet
			int result = 0;
			//insert bang Hoa don ban hang chi tiet,      
			//	var sanpham = await context.TblSanpham.FindAsync(donhangchitiet.Idsanpham);		
			// insert 1 dòng			
			await context.CvjpDonHangChiTiets.AddAsync(donhangchitiet);
			result = await context.SaveChangesAsync();
			// kiểm tra Post san pham thanh cong          
			donhang = await UpdateDonHangCVJPAsync(donhang, context);
			return donhang;
		}
		public async Task<CvjpDonHang> UpdateDonHangCVJPAsync(CvjpDonHang _donhang, MTLContext context)
		{
			//update lại tổng tiền trên HD
			CvjpDonHang donhang = new CvjpDonHang();
			var items_hd = (from o in context.CvjpDonHangChiTiets
							where o.Mahd == _donhang.Mahd
							select new { o.Mahd, o.Thanhtien }
							).ToList();
			var sum = items_hd.Select(c => c.Thanhtien).Sum();
			donhang = context.CvjpDonHangs.Single(a => a.Mahd == _donhang.Mahd);
			donhang.Mahd = _donhang.Mahd;
			donhang.Idkhachhang = _donhang.Idkhachhang;
			donhang.Ghichu = _donhang.Ghichu;
			donhang.Idnhanviendathang = _donhang.Idnhanviendathang;
			donhang.Idnhanvienxuathang = _donhang.Idnhanvienxuathang;
			donhang.Idphuongthucdonggoi = _donhang.Idphuongthucdonggoi;
			donhang.Idphuphi = _donhang.Idphuphi;
			donhang.Idhinhthucthanhtoan = _donhang.Idhinhthucthanhtoan;
			donhang.Idtrangthaidonhang = _donhang.Idtrangthaidonhang;
			donhang.Ngaydatdon = _donhang.Ngaydatdon;
			donhang.Ngayxuatdon = _donhang.Ngayxuatdon;
			donhang.Ngaynhangiao = _donhang.Ngaynhangiao;
			donhang.Idgionhangiao = _donhang.Idgionhangiao;
			donhang.Phigiaohang = _donhang.Phigiaohang;
			donhang.Phuphi = _donhang.Phuphi;
			donhang.Tongcong = sum;

			if (donhang.Idhinhthucthanhtoan == 2) // Chuyển khoản trước (Chỉ chuyển khoản trước 2 ngày)
			{
				donhang.Phigiaohang = 0;
			}
			else
			{
				// Nghiệp vụ giao hàng thu tiền
				//Dưới 30000¥ là 400¥
				//Trên 30000¥ là 600¥
				//Trên 100000¥ là 1000¥
				if (donhang.Tongcong < 30000)
					donhang.Phigiaohang = 400;
				else if (donhang.Tongcong >= 30000 && donhang.Tongcong < 100000)
					donhang.Phigiaohang = 600;
				else if (donhang.Tongcong >= 100000)
					donhang.Phigiaohang = 1000;
			}

			// Tính tổng đơn
			donhang.Tongdonhang = donhang.Tongcong + _donhang.Phigiaohang + _donhang.Phuphi;
			donhang.Lastupdate = _donhang.Lastupdate;

			donhang.Sokienhang = _donhang.Sokienhang;
			donhang.Hoahongmoikien = _donhang.Hoahongmoikien;
			donhang.Tongtienhoahong = donhang.Sokienhang * donhang.Hoahongmoikien;

			int result = await context.SaveChangesAsync();
			return donhang;
		}

		public async Task<List<CVJP_GetDonHangChiTietResult>> CVJP_GetDonHangChiTietAsync(string mahd)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			IQueryable<CVJP_GetDonHangChiTietResult> donhangchitiet;
			donhangchitiet = context.CVJP_GetDonHangChiTiet(mahd);
			return await donhangchitiet.ToListAsync();
		}

		public async Task<CvjpDonHang> CVJP_GetDonHangAsync(string MaHD)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			var donhang = await context.CvjpDonHangs.FindAsync(MaHD);
			return donhang;
		}

		public async Task<CvjpDonHang> CVJP_DeleteDonHangAsync(CvjpDonHang donhang)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			var donhangchitiet = from r in context.CvjpDonHangChiTiets
								 where (r.Mahd == donhang.Mahd)
								 select r;
			if (donhangchitiet != null)
			{
				context.CvjpDonHangChiTiets.RemoveRange(donhangchitiet);
				await context.SaveChangesAsync();
			}
			CvjpDonHang _donhang = await context.CvjpDonHangs.FindAsync(donhang.Mahd);
			if (_donhang != null)
			{
				context.CvjpDonHangs.Remove(_donhang);
				await context.SaveChangesAsync();
			}
			return _donhang;
		}

		public async Task<List<TrangThaiDonHang>> CVJP_GetTrangThaiDonHangAsync()
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			return await context.TrangThaiDonHangs.ToListAsync();
		}

		public async Task<List<CVJP_GetAllDonHangResult>> CVJP_GetAllDonHangAsync(int idcompany, int iduserlogin, string OrderBy, bool isLocNgay, string TuNgay, string DenNgay)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			IQueryable<CVJP_GetAllDonHangResult> GetAllDonHang;
			GetAllDonHang = context.CVJP_GetAllDonHang(idcompany, iduserlogin);

			if (OrderBy == "Ngày hóa đơn") GetAllDonHang = GetAllDonHang.OrderByDescending(b => b.ngaydatdon);
			else if (OrderBy == "Ngày cập nhật") GetAllDonHang = GetAllDonHang.OrderByDescending(b => b.lastupdate);

			if (isLocNgay)
			{
				CultureInfo provider = new CultureInfo("en-CA");
				DateTime c_tungay = DateTime.ParseExact(TuNgay, "MM/dd/yyyy", provider);
				DateTime c_denngay = DateTime.ParseExact(DenNgay, "MM/dd/yyyy", provider);
				c_denngay = c_denngay.AddHours(23).AddMinutes(59).AddSeconds(59);
				GetAllDonHang = GetAllDonHang.Where(b => b.ngaydatdon >= c_tungay
								&& b.ngaydatdon <= c_denngay);
			}
			return await GetAllDonHang.ToListAsync();
		}

		public async Task<List<CvjpGioNhanGiao>> CVJP_GetGioNhanGiaoAsync()
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			return await context.CvjpGioNhanGiaos.ToListAsync();
		}

		public async Task<List<HinhThucThanhToan>> CVJP_GetHinhThucThanhToanAsync()
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			return await context.HinhThucThanhToans.ToListAsync();
		}

		public async Task<List<CvjpPhuPhi>> CVJP_GetPhuPhiAsync()
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			return await context.CvjpPhuPhis.ToListAsync();
		}

		public async Task<CvjpDonHang> DeleteSanPhamTrenDonHangAsync(DatHangResult dathang)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			CvjpDonHang donhang = dathang.CVJP_DonHang;
			CvjpDonHangChiTiet donhangchitiet = dathang.CVJP_DonHangChiTiet;

			var chitiet = await context.CvjpDonHangChiTiets.FindAsync(donhangchitiet.Id);
			if (chitiet != null)
			{
				context.CvjpDonHangChiTiets.Remove(chitiet);
				await context.SaveChangesAsync();
			}
			donhang = await UpdateDonHangCVJPAsync(donhang, context);
			return donhang;
		}

		public async Task<CvjpDonHang> CVJP_SuaSPDatHangAsync(DatHangResult dathang)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			CvjpDonHang donhang = dathang.CVJP_DonHang;
			CvjpDonHangChiTiet donhangchitiet = dathang.CVJP_DonHangChiTiet;
			int result = 0;
			// Check san pham trong don hang va update
			var _spct = context.CvjpDonHangChiTiets.FirstOrDefault(hd => hd.Id == donhangchitiet.Id);
			if (_spct != null)
			{
				_spct.Sl = donhangchitiet.Sl;
				_spct.Dongia = donhangchitiet.Dongia;
				_spct.Thanhtien = donhangchitiet.Thanhtien;
				result = await context.SaveChangesAsync();
			}
			// kiểm tra Post san pham thanh cong          
			donhang = await UpdateDonHangCVJPAsync(donhang, context);
			return donhang;
		}

		public async Task<List<CVJP_GetDonHangResult>> CVJP_GetDonHangByMaHDAsync(string MaHD)
		{
			//IQueryable<CVJP_GetDonHangResult> donhang = await context.CVJP_GetDonHang(MaHD);          
			//         return await donhang.ToListAsync();
			await using var context = await _contextFactory.CreateDbContextAsync();
			IQueryable<CVJP_GetDonHangResult>? donhang;
			donhang = context.CVJP_GetDonHang(MaHD);
			return await donhang.ToListAsync();
		}





		public async Task<List<MTL_GetAllInvoice_byKhachHangResult>> GetAllInvoiceByKhachHangAsync(int idcompany, int idkhachhang)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			IQueryable<MTL_GetAllInvoice_byKhachHangResult> invoice;
			invoice = context.MTL_GetAllInvoice_byKhachHang(idcompany, idkhachhang)
				.OrderByDescending(b => b.id);
			return await invoice.ToListAsync();
		}

		/// <summary>
		/// Lấy tách nguồn thanh toán (tiền mặt / CK / CK dự kiến) theo lô mã HĐ — phục vụ danh sách hóa đơn.
		/// </summary>
		public async Task<Dictionary<string, InvoicePaymentBreakdownDto>> GetInvoicePaymentBreakdownsAsync(int idcompany, IEnumerable<string?> mahds)
		{
			var keys = mahds?
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.Select(s => s!.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList() ?? new List<string>();

			var result = new Dictionary<string, InvoicePaymentBreakdownDto>(StringComparer.OrdinalIgnoreCase);
			if (keys.Count == 0)
				return result;

			await using var context = await _contextFactory.CreateDbContextAsync();
			const int batchSize = 500;
			for (int i = 0; i < keys.Count; i += batchSize)
			{
				var batch = keys.Skip(i).Take(batchSize).ToList();
				var rows = await context.TblHoaDonBanHangs.AsNoTracking()
					.Where(h => h.Idcompany == idcompany && batch.Contains(h.Mahd))
					.Select(h => new { h.Mahd, h.Tienmat, h.Tienchuyenkhoan, h.Tienchuyenkhoandukien })
					.ToListAsync();

				foreach (var r in rows)
				{
					if (string.IsNullOrEmpty(r.Mahd)) continue;
					result[r.Mahd] = new InvoicePaymentBreakdownDto
					{
						Tienmat = r.Tienmat,
						Tienchuyenkhoan = r.Tienchuyenkhoan,
						Tienchuyenkhoandukien = r.Tienchuyenkhoandukien,
					};
				}
			}

			return result;
		}

		/// <summary>
		/// Sau khi tính lại <see cref="TblHoaDonBanHang.Tongtiensauvat"/> từ chi tiết dòng: giữ hình thức thanh toán trên phiếu
		/// và tách lại TM / CK / trả góp cho khớp tổng mới. Trước đây luôn ép 100% tiền mặt nên <c>idhinhthucthanhtoan</c> (vd. Trả góp)
		/// không khớp số tiền và danh sách hóa đơn hiển thị sai.
		/// </summary>
		static void ApplyPaymentSplitAfterLineItemsChange(TblHoaDonBanHang hoadon, TblHoaDonBanHang fromForm)
		{
			var total = hoadon.Tongtiensauvat ?? 0;
			var htt = fromForm.Idhinhthucthanhtoan ?? MTLEnum.HinhThucThanhToan.TienMat;
			if (htt == MTLEnum.HinhThucThanhToan.Quetthe)
				htt = MTLEnum.HinhThucThanhToan.ChuyenKhoan;

			if (total < 0.01)
			{
				hoadon.Tienmat = 0;
				hoadon.Tienchuyenkhoan = 0;
				hoadon.Tienchuyenkhoandukien = 0;
				hoadon.Thanhtoan = 0;
				hoadon.Conlai = 0;
				hoadon.Ngaydukienchuyenkhoan = null;
				hoadon.Idhinhthucthanhtoan = htt;
				hoadon.Idbank = null;
				return;
			}

			switch (htt)
			{
				case MTLEnum.HinhThucThanhToan.TienMat:
					hoadon.Tienmat = total;
					hoadon.Tienchuyenkhoan = 0;
					hoadon.Tienchuyenkhoandukien = 0;
					hoadon.Thanhtoan = total;
					hoadon.Conlai = 0;
					hoadon.Ngaydukienchuyenkhoan = null;
					break;
				case MTLEnum.HinhThucThanhToan.ChuyenKhoan:
					hoadon.Tienmat = 0;
					hoadon.Tienchuyenkhoan = total;
					hoadon.Tienchuyenkhoandukien = 0;
					hoadon.Thanhtoan = total;
					hoadon.Conlai = 0;
					hoadon.Ngaydukienchuyenkhoan = null;
					break;
				case MTLEnum.HinhThucThanhToan.Tragop:
				{
					var ckPart = fromForm.Tienchuyenkhoandukien ?? fromForm.Conlai ?? 0;
					if (ckPart > total) ckPart = total;
					if (ckPart < 0) ckPart = 0;
					hoadon.Tienchuyenkhoan = 0;
					hoadon.Tienchuyenkhoandukien = ckPart;
					hoadon.Tienmat = total - ckPart;
					hoadon.Thanhtoan = hoadon.Tienmat;
					hoadon.Conlai = ckPart;
					hoadon.Ngaydukienchuyenkhoan = fromForm.Ngaydukienchuyenkhoan;
					break;
				}
				case MTLEnum.HinhThucThanhToan.TienMatVaChuyenKhoan:
				{
					var tm0 = fromForm.Tienmat ?? 0;
					var ck0 = fromForm.Tienchuyenkhoan ?? 0;
					var oldSum = tm0 + ck0;
					double ck;
					if (oldSum >= 0.01)
						ck = total * (ck0 / oldSum);
					else
						ck = Math.Min(ck0, total);
					if (ck > total) ck = total;
					ck = Math.Round(ck, 0);
					hoadon.Tienchuyenkhoan = ck;
					hoadon.Tienmat = total - ck;
					hoadon.Tienchuyenkhoandukien = 0;
					hoadon.Thanhtoan = total;
					hoadon.Conlai = 0;
					hoadon.Ngaydukienchuyenkhoan = null;
					break;
				}
				default:
					hoadon.Tienmat = total;
					hoadon.Tienchuyenkhoan = 0;
					hoadon.Tienchuyenkhoandukien = 0;
					hoadon.Thanhtoan = total;
					hoadon.Conlai = 0;
					hoadon.Ngaydukienchuyenkhoan = null;
					htt = MTLEnum.HinhThucThanhToan.TienMat;
					break;
			}

			hoadon.Idhinhthucthanhtoan = htt;

			var tienCkDaThu = hoadon.Tienchuyenkhoan ?? 0;
			var tienCkDuKien = hoadon.Tienchuyenkhoandukien ?? 0;
			var httSaved = hoadon.Idhinhthucthanhtoan ?? 0;
			var canBank = tienCkDaThu > 0.009 || tienCkDuKien > 0.009 || httSaved == 2 || httSaved == 3 || httSaved == 4 || httSaved == 5;
			if (canBank)
				hoadon.Idbank = fromForm.Idbank;
			else
				hoadon.Idbank = null;
		}

		/// <summary>
		/// Chiến dịch ZNS nhắc vệ sinh: HĐ đã xử lý, có SĐT + tên KH, có thẻ trong tập tag, lọc theo ngày HĐ (ngaytraphieu).
		/// </summary>
		public async Task<List<MTL_GetAllInvoiceResult>> GetInvoicesForZnsVeSinhCampaignAsync(
		int idcompany,
		bool autoOneYearAnniversary,
		DateTime? customTuNgay,
		DateTime? customDenNgay,
		IReadOnlyList<int> filterTagIds)
		{
			await using var context = await _contextFactory.CreateDbContextAsync();
			if (filterTagIds == null || filterTagIds.Count == 0)
				return new List<MTL_GetAllInvoiceResult>();

			var tagSet = filterTagIds.ToHashSet();

			IQueryable<MTL_GetAllInvoiceResult> q = context.MTL_GetAllInvoice(idcompany)
				.Where(iv => iv.isDichVuDaXuLy == true)
				.Where(iv => iv.idLoaiPhieu != 4)
				.Where(iv => !string.IsNullOrWhiteSpace(iv.sodienthoai))
				.Where(iv => !string.IsNullOrWhiteSpace(iv.tenkh))
				.Where(iv => iv.ngaytraphieu != null);

			if (autoOneYearAnniversary)
			{
				// Cửa sổ 10–13 tháng trước hôm nay — thay vì đúng ngày-tròn-1-năm (tránh bỏ sót khách).
				var windowStart = DateTime.Today.AddMonths(-13).Date;
				var windowEnd   = DateTime.Today.AddMonths(-10).Date.AddDays(1).AddTicks(-1);
				q = q.Where(iv => iv.ngaytraphieu >= windowStart && iv.ngaytraphieu <= windowEnd);
			}
			else
			{
				if (customTuNgay == null || customDenNgay == null)
					return new List<MTL_GetAllInvoiceResult>();
				var tu = customTuNgay.Value.Date;
				var denEnd = customDenNgay.Value.Date.AddDays(1).AddTicks(-1);
				q = q.Where(iv => iv.ngaytraphieu >= tu && iv.ngaytraphieu <= denEnd);
			}

			var matchingMahd = await (
				from iv in q
				join tm in context.TblHoaDonBanHangTagMaps on iv.mahd equals tm.Mahd
				where tm.Tagid.HasValue && tagSet.Contains(tm.Tagid.Value)
				select iv.mahd
			).Distinct().ToListAsync();

			if (matchingMahd.Count == 0)
				return new List<MTL_GetAllInvoiceResult>();

			return await context.MTL_GetAllInvoice(idcompany)
				.Where(iv => matchingMahd.Contains(iv.mahd))
				.OrderByDescending(iv => iv.ngaytraphieu)
				.ToListAsync();
		}
	}

	/// <summary>Tách số tiền thanh toán trên hóa đơn (bảng TblHoaDonBanHang).</summary>
	public sealed class InvoicePaymentBreakdownDto
	{
		public double? Tienmat { get; set; }
		public double? Tienchuyenkhoan { get; set; }
		public double? Tienchuyenkhoandukien { get; set; }
	}
}
