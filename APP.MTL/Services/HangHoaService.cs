using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.APP.Models;

namespace MTL.APP.Services
{
	public class HangHoaService
	{
		private readonly MTLContext _context;
		public HangHoaService(MTLContext appDBContext)
		{
			_context = appDBContext;
		}

		// HÀNG HÓA
		public async Task<List<MTL_GetAllProductsResult>> GetAllProductAsync(int idcompany, string TonKho, int idloaihanghoadichvu, string OrderBy)
		{
			IQueryable<MTL_GetAllProductsResult> prods;
			prods = _context.MTL_GetAllProducts(idcompany, idloaihanghoadichvu);

			if (TonKho == "Còn hàng") prods = prods.Where(b => b.active == true);
			else if (TonKho == "Hết hàng") prods = prods.Where(b => b.active == false);
			// else if (TonKho == "Tất cả") prods = prods.Where(b => b.active == 3);
			//// Lọc Loại HHDV
			//prods = prods.Where(b => b.idloaihanghoadichvu == idloaihanghoadichvu);   
			if (OrderBy == "Ngày tạo") prods = prods.OrderByDescending(b => b.beginupdate);
			else if (OrderBy == "Ngày cập nhật") prods = prods.OrderByDescending(b => b.lastupdate);
			//else if (OrderBy == "Giá tăng dần") prods = prods.OrderBy(b => b.giaban);
			//else if (OrderBy == "Giá giảm dần") prods = prods.OrderByDescending(b => b.giaban);
			return await prods.ToListAsync();
		}

		public async Task<List<MTL_GetAllProductsInCompanyResult>> GetAllProductCompanyAsync(int idcompany, string TonKho, int idnhomhang, string OrderBy)
		{
			IQueryable<MTL_GetAllProductsInCompanyResult> prods;
			prods = _context.MTL_GetAllProductsInCompany(idcompany);

			if (TonKho == "Còn hàng") prods = prods.Where(b => b.active == true);
			else if (TonKho == "Hết hàng") prods = prods.Where(b => b.active == false);
			//lọc nhóm hàng  
			if (idnhomhang != 0) prods = prods.Where(b => b.idnhomhang == idnhomhang);
			if (OrderBy == "Ngày tạo") prods = prods.OrderByDescending(b => b.lastupdate);
			else if (OrderBy == "Giá tăng dần") prods = prods.OrderBy(b => b.giaban);
			else if (OrderBy == "Giá giảm dần") prods = prods.OrderByDescending(b => b.giaban);
			return await prods.ToListAsync();
		}


		public async Task<TblSanpham> GetProductAsync(int id)
		{
			var sanpham = await _context.TblSanphams.FindAsync(id);
			return sanpham;
		}
		public async Task<TblSanpham> CreateProductAsync(TblSanpham sanpham)
		{
			_context.TblSanphams.Add(sanpham);
			await _context.SaveChangesAsync();
			return await Task.FromResult(sanpham);
		}
		public async Task<int> DeleteProductAsync(int id)
		{
			await _context.TblSanphams.Where(_ => _.Id == id).ExecuteDeleteAsync();
			return id;
		}

		public async Task<TblSanpham> UpdateProductAsync(TblSanpham sp)
		{
			TblSanpham spupdate = new TblSanpham();
			spupdate = _context.TblSanphams.Single(a => a.Id == sp.Id);
			spupdate.Id = sp.Id;
			spupdate.Masp = sp.Masp;
			spupdate.Tensp = sp.Tensp;
			spupdate.Mavach = sp.Mavach;
			spupdate.Idnhomhang = sp.Idnhomhang;
			spupdate.Idthuonghieu = sp.Idthuonghieu;
			spupdate.Idvitri = sp.Idvitri;
			spupdate.Giavon = sp.Giavon;
			spupdate.Giaban = sp.Giaban;
			spupdate.Giathuong = sp.Giathuong;
			spupdate.Trongluong = sp.Trongluong;
			spupdate.Tonkho = sp.Tonkho;
			spupdate.Hinhanh1 = sp.Hinhanh1;
			spupdate.Hinhanh2 = sp.Hinhanh2;
			spupdate.Hinhanh3 = sp.Hinhanh3;
			spupdate.Hinhanh4 = sp.Hinhanh4;
			spupdate.Hinhanh5 = sp.Hinhanh5;
			spupdate.Pic00 = sp.Pic00;
			spupdate.Video = sp.Video;
			spupdate.Iddonvitinh = sp.Iddonvitinh;
			spupdate.Beginupdate = sp.Beginupdate;
			spupdate.Lastupdate = sp.Lastupdate;
			spupdate.Mota = sp.Mota;
			spupdate.Motachitiet = sp.Motachitiet;
			spupdate.Ghichu = sp.Ghichu;
			spupdate.DinhmuctonItnhat = sp.DinhmuctonItnhat;
			spupdate.DinhmuctonNhieunhat = sp.DinhmuctonNhieunhat;
			spupdate.Active = sp.Active;
			spupdate.IdBaoHanh = sp.IdBaoHanh;
			spupdate.Idloaihanghoadichvu = sp.Idloaihanghoadichvu;
			spupdate.Isdelete = sp.Isdelete;
			spupdate.Model = sp.Model;
			spupdate.Maspnew = sp.Maspnew;
			spupdate.Iduserlastupdate = sp.Iduserlastupdate;
			spupdate.Iduserlastcreate = sp.Iduserlastcreate;
			spupdate.Link = sp.Link;
			spupdate.Cpu = sp.Cpu;
			spupdate.Ram = sp.Ram;
			spupdate.Ssd = sp.Ssd;
			spupdate.Vga = sp.Vga;
			spupdate.WooId = sp.WooId;
			spupdate.WooRegularPrice = sp.WooRegularPrice;
			spupdate.WooSlug = sp.WooSlug;
			spupdate.Idcompany = sp.Idcompany;
			spupdate.TrongluongCold = sp.TrongluongCold;
			spupdate.TrongluongDry = sp.TrongluongDry;
            spupdate.WooBlockSanpham = sp.WooBlockSanpham;

            int result = await _context.SaveChangesAsync();
			return spupdate;
		}

		// HÀNG HÓA THÀNH PHẦN
		public async Task<List<MTL_GetSanPhamThanhPhanResult>> GetHangHoaThanhPhanAsync(int idsanpham, int idcompany)
		{
			// Select * from MTL_GetSanPhamThanhPhan(720) ORDER BY CASE WHEN [order] IS NULL THEN 1 ELSE 0 END, [order]
			IQueryable<MTL_GetSanPhamThanhPhanResult> hanghoathanhphan;
			hanghoathanhphan = _context.MTL_GetSanPhamThanhPhan(idsanpham, idcompany)
			   .OrderBy(p => (p.order == null) ? 1 : 0)
			   .OrderBy(p => p.id);
			return await hanghoathanhphan.ToListAsync();
		}

		public async Task<bool> SaveHangHoaThanhPhanAsync(List<MTL_GetSanPhamThanhPhanResult> ListHHTP, int idsp)
		{
			int result = 0;
			TblSanpham sp = new TblSanpham();
			// DELETE FROM dbo.tbl_hanghoathanhphan WHERE IDSANPHAM={0}
			await _context.TblHanghoathanhphans.Where(x => x.Idsanpham == idsp).ExecuteDeleteAsync();
			foreach (MTL_GetSanPhamThanhPhanResult hhtp in ListHHTP)
			{
				TblHanghoathanhphan hh = new TblHanghoathanhphan();
				hh.Idsanpham = idsp;
				hh.Tensp = hhtp.tensp;
				hh.Idhanghoathanhphan = hhtp.idhanghoathanhphan;
				hh.Issanphamao = hhtp.issanphamao;
				hh.Sl = hhtp.sl;
				hh.Giaban = hhtp.giaban;
				hh.Thanhtien = hhtp.thanhtien;
				hh.Idbaohanh = hhtp.idbaohanh;
				hh.Chietkhau = hhtp.chietkhau;
				hh.Beginupdate = hhtp.beginupdate;
				hh.Lastupdate = hhtp.lastupdate;
				await _context.TblHanghoathanhphans.AddAsync(hh);
				result = await _context.SaveChangesAsync();
			}
			// var sanpham = await _context.TblSanpham.FindAsync(idsp);
			return true;
		}

		//LOAI HANG HOA DICH VU
		public async Task<List<TblLoaihanghoadichvu>> GetLoaiHHDVAllAsync()
		{
			return await _context.TblLoaihanghoadichvus
				  .ToListAsync();
		}
		//NHÓM HÀNG
		public async Task<List<MTL_GetAllNhomHangResult>> MTL_GetAllNhomHangAsync(int idloaihanghoadichvu, int idcompany)
		{
			return await _context.MTL_GetAllNhomHang(idloaihanghoadichvu, idcompany)
				  .ToListAsync();
		}

		public async Task<List<MTL_GetAllNhomHangResult>> MTL_GetAllNhomHangHienThiAsync(int idloaihanghoadichvu, int idcompany)
		{
			return await _context.MTL_GetAllNhomHang(idloaihanghoadichvu, idcompany)
				  .Where(b => b.active == true)
				  .ToListAsync();
		}

		public async Task<IEnumerable<TblNhomhang>> GetNhomHangAsync()
		{
			return await _context.TblNhomhangs.ToListAsync();
		}
		public async Task<TblNhomhang> CreateNhomHangAsync(TblNhomhang nhomhang)
		{
			_context.TblNhomhangs.Add(nhomhang);
			await _context.SaveChangesAsync();
			return nhomhang;
		}
		public async Task<TblNhomhang> UpdateNhomHangAsync(TblNhomhang nh)
		{
			int result = 0;
			TblNhomhang nhomhang_up = new TblNhomhang();
			var _nhomhang = _context.TblNhomhangs.FirstOrDefault(hd => hd.Id == nh.Id);
			if (_nhomhang != null)
			{
				_nhomhang.Nhomhang = nh.Nhomhang;
				_nhomhang.Viettat = nh.Viettat;
				_nhomhang.WooId = nh.WooId;
				_nhomhang.Order = nh.Order;
				_nhomhang.Active = nh.Active;
				_nhomhang.Lastupdate = nh.Lastupdate;
				_nhomhang.Iduserlastupdate = nh.Iduserlastupdate;
				_nhomhang.Parent = nh.Parent;
				_nhomhang.Idloaihanghoadichvu = nh.Idloaihanghoadichvu;
				_nhomhang.Tendanhmucrutgon = nh.Tendanhmucrutgon;
				_nhomhang.Link = nh.Link;
				result = await _context.SaveChangesAsync();
			}
			return _nhomhang;
		}
		public async Task<ActionResult<TblNhomhang>> DeleteNhomHangAsync(int id)
		{
			var nhomhang = await _context.TblNhomhangs.FindAsync(id);
			if (nhomhang != null)
			{
				_context.TblNhomhangs.Remove(nhomhang);
				await _context.SaveChangesAsync();
			}
			return nhomhang;
		}

		// THƯƠNG HIỆU
		public async Task<List<MTL_GetAllThuongHieuResult>> GetThuongHieuAllAsync(int idcompany)
		{
			return await _context.MTL_GetAllThuongHieu(idcompany)
				  .OrderBy(b => b.thuonghieu)
				  .ToListAsync();
		}
		public async Task<TblThuonghieu> CreateThuongHieuAsync(TblThuonghieu thuonghieu)
		{
			_context.TblThuonghieus.Add(thuonghieu);
			await _context.SaveChangesAsync();
			return thuonghieu;
		}
		public async Task<ActionResult<TblThuonghieu>> DeleteThuongHieuAsync(int id)
		{
			var thuonghieu = await _context.TblThuonghieus.FindAsync(id);
			if (thuonghieu != null)
			{
				_context.TblThuonghieus.Remove(thuonghieu);
				await _context.SaveChangesAsync();
			}
			return thuonghieu;
		}
		public async Task<TblThuonghieu> UpdateThuongHieuAsync(TblThuonghieu th)
		{
			int result = 0;
			TblThuonghieu thuonghieu_up = new TblThuonghieu();
			var _thuonghieu = _context.TblThuonghieus.FirstOrDefault(hd => hd.Id == th.Id);
			if (_thuonghieu != null)
			{
				//sửa
				//update
				//user_up = _context.TblUsers.Single(a => a.Id == _khachhang.Id);               
				_thuonghieu.Thuonghieu = th.Thuonghieu;
				_thuonghieu.Viettat = th.Viettat;
				_thuonghieu.WooId = th.WooId;
				_thuonghieu.Order = th.Order;
				_thuonghieu.Active = th.Active;
				_thuonghieu.Lastupdate = th.Lastupdate;
				result = await _context.SaveChangesAsync();

			}
			return _thuonghieu;
		}

		// BẢO HÀNH
		public async Task<List<TblBaoHanh>> GetBaoHanhAsync()
		{
			return await _context.TblBaoHanhs.OrderBy(b => b.Order)
				  .ToListAsync();
		}

		// VỊ TRÍ
		public async Task<List<TblVitri>> GetViTriAsync(int idcompany)
		{
			return await _context.TblVitris
				  .Where(b => b.Idcompany == idcompany)
				  .ToListAsync();
		}

		// ĐƠN VỊ TÍNH
		public async Task<List<TblDonvitinh>> GetDonViTinhAsync()
		{
			return await _context.TblDonvitinhs
				  .ToListAsync();
		}

		//TAG
		public async Task<List<MTL.APP.Models.Tag>> GetTagAsync(int idcompany)
		{
			return await _context.Tags
				  .Where(b => b.Idcompany == idcompany)
				  .ToListAsync();
		}
		public async Task<IEnumerable<MTL.APP.Models.TblHoaDonBanHangTagMap>> GetHoaDonTagMapAsync(string mahd)
		{
			return await _context.TblHoaDonBanHangTagMaps
				.Where(b => b.Mahd == mahd)
				.ToListAsync();
		}


        // bank
        public async Task<List<TblBank>> GetBankAsync(int idcompany)
        {
            return await _context.TblBanks
                  .Where(b => b.Idcompany == idcompany)
                  .ToListAsync();
        }




    }
}
