using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.APP.Models;

namespace MTL.APP.Services
{
	public class CVJPService : ControllerBase
	{
		private readonly MTLContext _context;
		public CVJPService(MTLContext context)
		{
			_context = context;
		}

		public async Task<List<CVJP_GetAllProductsResult>> CVJP_GetAllProductAsync(int idcompany, string TonKho, int idloaihanghoadichvu, int idnhomhang, string OrderBy)
		{
			IQueryable<CVJP_GetAllProductsResult> prods;
			prods = _context.CVJP_GetAllProducts(idcompany, idloaihanghoadichvu, idnhomhang);

			if (TonKho == "Còn hàng") prods = prods.Where(b => b.active == true);
			else if (TonKho == "Hết hàng") prods = prods.Where(b => b.active == false);
			// else if (TonKho == "Tất cả") prods = prods.Where(b => b.active == 3);
			//// Lọc Loại HHDV
			//prods = prods.Where(b => b.idloaihanghoadichvu == idloaihanghoadichvu);   
			if (OrderBy == "Ngày tạo") prods = prods.OrderByDescending(b => b.lastupdate);
			else if (OrderBy == "Giá tăng dần") prods = prods.OrderBy(b => b.giaban);
			else if (OrderBy == "Giá giảm dần") prods = prods.OrderByDescending(b => b.giaban);
			return await prods.ToListAsync();
		}

		public async Task<CvjpSanpham> CVJP_UpdateProductAsync(CvjpSanpham sp)
		{
			CvjpSanpham spupdate = new CvjpSanpham();
			spupdate = _context.CvjpSanphams.Single(a => a.Id == sp.Id);
			spupdate.Id = sp.Id;
			spupdate.Tensp = sp.Tensp;
			spupdate.Idnhomhang = sp.Idnhomhang;
			spupdate.Idvitri = sp.Idvitri;
			spupdate.Giavon = sp.Giavon;
			spupdate.Giaban = sp.Giaban;
			spupdate.Pic00 = sp.Pic00;
			spupdate.Iddonvitinh = sp.Iddonvitinh;
			spupdate.Beginupdate = sp.Beginupdate;
			spupdate.Lastupdate = sp.Lastupdate;
			spupdate.Ghichu = sp.Ghichu;
			spupdate.Active = sp.Active;
			spupdate.Idloaihanghoadichvu = sp.Idloaihanghoadichvu;
			spupdate.Isdelete = sp.Isdelete;
			spupdate.Iduserlastupdate = sp.Iduserlastupdate;
			spupdate.Iduserlastcreate = sp.Iduserlastcreate;
			spupdate.Idcompany = sp.Idcompany;
			spupdate.TrongluongCold = sp.TrongluongCold;
			spupdate.TrongluongDry = sp.TrongluongDry;
			int result = await _context.SaveChangesAsync();
			return spupdate;
		}

		public async Task<CvjpSanpham> CVJP_CreateProductAsync(CvjpSanpham sanpham)
		{
			_context.CvjpSanphams.Add(sanpham);
			await _context.SaveChangesAsync();
			return await Task.FromResult(sanpham);
		}

		public async Task<ActionResult<CvjpSanpham>> CVJP_DeleteProductAsync(int id)
		{
			var sanpham = await _context.CvjpSanphams.FindAsync(id);
			if (sanpham == null)
			{
				return NotFound();
			}
			_context.CvjpSanphams.Remove(sanpham);
			await _context.SaveChangesAsync();
			return sanpham;
		}




		//VietNhatTapHoa       
		public async Task<string> CVJP_CreateMaPhieuDatHangAsync(int idnhanviendathang)
		{
			string MaHD = "";
			MaHD = _context.Database
				.SqlQueryRaw<string>(string.Format("select [dbo].[CVJP_CreateMaPhieuDatHang]('{0}')  as 'Value'", idnhanviendathang))
				.FirstOrDefault().Replace(" ", string.Empty);
			return MaHD;
		}

		public async Task<CvjpDonHang> CVJP_ThemSPVaoPhieuDatHangAsync(DatHangResult dathang)
		{
			CvjpDonHang donhang = dathang.CVJP_DonHang;
			CvjpDonHangChiTiet donhangchitiet = dathang.CVJP_DonHangChiTiet;
			bool HoaDonExists;
			int result_hd = 0;
			HoaDonExists = _context.CvjpDonHangs.Any(hd => hd.Mahd == donhang.Mahd);
			if (HoaDonExists)
			{
				// đã tồn tại Hóa đơn
			}
			else
			{
				// chưa tồn tại hóa đơn, thì thêm mới
				await _context.CvjpDonHangs.AddAsync(donhang);
				result_hd = await _context.SaveChangesAsync();
			}
			//insert bang Hoa don ban hang chi tiet
			int result = 0;
			//insert bang Hoa don ban hang chi tiet,      
			//	var sanpham = await _context.TblSanpham.FindAsync(donhangchitiet.Idsanpham);		
			// insert 1 dòng			
			await _context.CvjpDonHangChiTiets.AddAsync(donhangchitiet);
			result = await _context.SaveChangesAsync();
			// kiểm tra Post san pham thanh cong          
			donhang = await UpdateDonHangCVJPAsync(donhang);
			return donhang;
		}

		public async Task<CvjpDonHang> UpdateDonHangCVJPAsync(CvjpDonHang _donhang)
		{
			//update lại tổng tiền trên HD
			CvjpDonHang donhang = new CvjpDonHang();
			var items_hd = (from o in _context.CvjpDonHangChiTiets
							where o.Mahd == _donhang.Mahd
							select new { o.Mahd, o.Thanhtien }
							).ToList();
			var sum = items_hd.Select(c => c.Thanhtien).Sum();
			donhang = _context.CvjpDonHangs.Single(a => a.Mahd == _donhang.Mahd);
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
			donhang.Tongdonhang = donhang.Tongcong + _donhang.Phigiaohang + _donhang.Phuphi;
			donhang.Lastupdate = _donhang.Lastupdate;
			int result = await _context.SaveChangesAsync();
			return donhang;
		}

		public async Task<List<CVJP_GetDonHangChiTietResult>> CVJP_GetDonHangChiTietAsync(string mahd)
		{
			IQueryable<CVJP_GetDonHangChiTietResult> donhangchitiet;
			donhangchitiet = _context.CVJP_GetDonHangChiTiet(mahd);
			return await donhangchitiet.ToListAsync();
		}

		public async Task<CvjpDonHang> CVJP_GetDonHangAsync(string MaHD)
		{
			var donhang = await _context.CvjpDonHangs.FindAsync(MaHD);
			return donhang;
		}

		public async Task<CvjpDonHang> CVJP_DeleteDonHangAsync(CvjpDonHang donhang)
		{
			var donhangchitiet = from r in _context.CvjpDonHangChiTiets
								 where (r.Mahd == donhang.Mahd)
								 select r;
			if (donhangchitiet != null)
			{
				_context.CvjpDonHangChiTiets.RemoveRange(donhangchitiet);
				await _context.SaveChangesAsync();
			}
			CvjpDonHang _donhang = await _context.CvjpDonHangs.FindAsync(donhang.Mahd);
			if (_donhang != null)
			{
				_context.CvjpDonHangs.Remove(_donhang);
				await _context.SaveChangesAsync();
			}
			return _donhang;
		}

		public async Task<List<TrangThaiDonHang>> CVJP_GetTrangThaiDonHangAsync()
		{
			return await _context.TrangThaiDonHangs.ToListAsync();
		}

		public async Task<List<CVJP_GetAllDonHangResult>> CVJP_GetAllDonHangAsync(int idcompany, int iduserlogin, string OrderBy, bool isLocNgay, string TuNgay, string DenNgay)
		{
			IQueryable<CVJP_GetAllDonHangResult> GetAllDonHang;
			GetAllDonHang = _context.CVJP_GetAllDonHang(idcompany, iduserlogin);

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
			return await _context.CvjpGioNhanGiaos.ToListAsync();
		}
		public async Task<List<HinhThucThanhToan>> CVJP_GetHinhThucThanhToanAsync()
		{
			return await _context.HinhThucThanhToans.ToListAsync();
		}

		public async Task<List<CvjpPhuPhi>> CVJP_GetPhuPhiAsync()
		{
			return await _context.CvjpPhuPhis.ToListAsync();
		}

		public async Task<CvjpDonHang> DeleteSanPhamTrenDonHangAsync(DatHangResult dathang)
		{
			CvjpDonHang donhang = dathang.CVJP_DonHang;
			CvjpDonHangChiTiet donhangchitiet = dathang.CVJP_DonHangChiTiet;

			var chitiet = await _context.CvjpDonHangChiTiets.FindAsync(donhangchitiet.Id);
			if (chitiet != null)
			{
				_context.CvjpDonHangChiTiets.Remove(chitiet);
				await _context.SaveChangesAsync();
			}
			donhang = await UpdateDonHangCVJPAsync(donhang);
			return donhang;
		}

		public async Task<CvjpDonHang> CVJP_SuaSPDatHangAsync(DatHangResult dathang)
		{
			CvjpDonHang donhang = dathang.CVJP_DonHang;
			CvjpDonHangChiTiet donhangchitiet = dathang.CVJP_DonHangChiTiet;
			int result = 0;
			// Check san pham trong don hang va update
			var _spct = _context.CvjpDonHangChiTiets.FirstOrDefault(hd => hd.Id == donhangchitiet.Id);
			if (_spct != null)
			{
				_spct.Sl = donhangchitiet.Sl;
				_spct.Dongia = donhangchitiet.Dongia;
				_spct.Thanhtien = donhangchitiet.Thanhtien;
				result = await _context.SaveChangesAsync();
			}
			// kiểm tra Post san pham thanh cong          
			donhang = await UpdateDonHangCVJPAsync(donhang);
			return donhang;
		}

		public async Task<List<CVJP_GetDonHangResult>> CVJP_GetDonHangByMaHDAsync(string MaHD)
		{
			IQueryable<CVJP_GetDonHangResult>? donhang;
			donhang = _context.CVJP_GetDonHang(MaHD);
			return await donhang.ToListAsync();
		}


	}
}