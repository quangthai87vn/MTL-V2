using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.APP.Authentication;
using MTL.APP.Models;

namespace MTL.APP.Services
{
	public class KhachHangService
	{
		private readonly MTLContext _context;
		public KhachHangService(MTLContext appDBContext)
		{
			_context = appDBContext;
		}

		public async Task<List<TblUser>> GetAllKhachHangAsync(int idcompany, int Idnhomkhachhang)
		{
			return await _context.TblUsers
				.Where(b => b.Idcompany == idcompany)
				.Where(b => b.Idnhomkhachhang == Idnhomkhachhang)
				.OrderByDescending(b => b.Lastupdate)
				.ToListAsync();
		}
		public async Task<IEnumerable<TblUser>> GetAllNhaCungCap()
		{
			return await _context.TblUsers
				.Where(b => b.Idnhomkhachhang == 3)
				.OrderByDescending(b => b.Lastupdate)
				.ToListAsync();
		}
		public async Task<IEnumerable<MTL_GetAllHoaDonCuResult>> GetAllHoaDonCu()
		{
			return await _context.MTL_GetAllHoaDonCu(true)
				.ToListAsync();
		}
		public async Task<IEnumerable<TblHoaDonChiTiet>> GetAllHoaDonChiTietCu(int IdhoaDon)
		{
			return await _context.TblHoaDonChiTiets
					.Where(b => b.IdhoaDon == IdhoaDon)
					.ToListAsync();
		}

		public async Task<IEnumerable<TblNhomkhachhang>> GetAllNhomKhachHang()
		{
			return await _context.TblNhomkhachhangs
				.ToListAsync();
		}
		public async Task<TblUser> GetKhachHangAsync(int? id)
		{
			var ur = await _context.TblUsers.FindAsync(id);
			if (ur == null)
			{
				return null;
			}
			else return ur;
		}

		/// <summary>
		/// Tìm khách hàng theo SĐT và IdCompany
		/// </summary>
		public async Task<TblUser?> GetKhachHangBySdtAsync(int idCompany, string soDienThoai)
		{
			return await Task.FromResult(_context.TblUsers
				.FirstOrDefault(x => x.Idcompany == idCompany 
					&& x.Dienthoai == soDienThoai 
					&& x.Idnhomkhachhang == 1));
		}

		public async Task<TblUser> SaveKhachHangAsync(TblUser ur, UserSession userSession)
		{
			int result = 0;
			TblUser user_up = new TblUser();

			// Check SDT xem có tồn tại không
			var _khachhang = _context.TblUsers.Where(b => b.Idcompany == userSession.company.Id)
				.FirstOrDefault(hd => hd.Dienthoai == ur.Dienthoai);
			if (_khachhang != null)
			{
				//sửa               
				_khachhang.Idnhomkhachhang = ur.Idnhomkhachhang;
				_khachhang.Makh = ur.Makh;
				_khachhang.Idusercreate = ur.Idusercreate;
				_khachhang.Beginupdate = ur.Beginupdate;
				_khachhang.Facebook = ur.Facebook;
				_khachhang.Dienthoai = ur.Dienthoai;
				_khachhang.Masothue = ur.Masothue;
				_khachhang.Website = ur.Website;
				_khachhang.Iduserlastupdate = ur.Iduserlastupdate;
				_khachhang.Lastupdate = DateTime.Now;
				_khachhang.Diachi = ur.Diachi;
				_khachhang.Ghichu = ur.Ghichu;
				_khachhang.Fullname = ur.Fullname;
				_khachhang.Email = ur.Email;
				_khachhang.Active = true;
				_khachhang.Idcompany = userSession.company.Id;
				_khachhang.Tenfacebook = ur.Tenfacebook;
				_khachhang.Mabuudien = ur.Mabuudien;
				_khachhang.Picture = ur.Picture;
				result = await _context.SaveChangesAsync();
				return _khachhang;
			}
			else
			{
				// thêm                
				ur.Active = true;
				ur.Idcompany = userSession.company.Id;
				ur.Beginupdate = DateTime.Now;
				ur.Lastupdate = DateTime.Now;
				ur.Idusercreate = ur.Iduserlastupdate = userSession.user.UserId;
				await _context.TblUsers.AddAsync(ur);
				int user_insert = await _context.SaveChangesAsync();
				return await Task.FromResult(ur);
			}
		}

		[HttpPost("DeleteKhachHangAsync")]
		public async Task<TblUser> DeleteKhachHangAsync(TblUser us)
		{
			TblUser _khachhang = await _context.TblUsers.FindAsync(us.Id);
			if (_khachhang != null)
			{
				_context.TblUsers.Remove(_khachhang);
				await _context.SaveChangesAsync();
			}
			return _khachhang;
		}

			/// <summary>
		/// Lưu hoặc cập nhật khách hàng từ Thu Hang Hoa.
		/// Check theo SĐT, nếu tồn tại thì cập nhật, không thì tạo mới.
		/// </summary>
		public async Task<TblUser> SaveOrUpdateKhachHangAsync(string tenKhachHang, string soDienThoai, string diaChi, string cmndCccd, int idCompany, int idNguoiTao)
		{
			if (string.IsNullOrWhiteSpace(soDienThoai))
				return null;

			var existing = _context.TblUsers
				.FirstOrDefault(x => x.Idcompany == idCompany && x.Dienthoai == soDienThoai && x.Idnhomkhachhang == 1);

			if (existing != null)
			{
				existing.Fullname = tenKhachHang ?? existing.Fullname;
				existing.Diachi = diaChi ?? existing.Diachi;
				existing.Makh = cmndCccd ?? existing.Makh;
				existing.Lastupdate = DateTime.Now;
				existing.Iduserlastupdate = idNguoiTao;
				await _context.SaveChangesAsync();
				return existing;
			}
			else
			{
				var newKhach = new TblUser
				{
					Idnhomkhachhang = 1,
					Idcompany = idCompany,
					Fullname = tenKhachHang ?? "Khách vãng lai",
					Dienthoai = soDienThoai,
					Diachi = diaChi,
					Makh = cmndCccd,
					Active = true,
					Beginupdate = DateTime.Now,
					Lastupdate = DateTime.Now,
					Idusercreate = idNguoiTao,
					Iduserlastupdate = idNguoiTao
				};
				_context.TblUsers.Add(newKhach);
				await _context.SaveChangesAsync();
				return newKhach;
			}
		}

		[HttpPost("CheckSDTKhachHangExitsAsync")]
		public async Task<TblUser> CheckSDTKhachHangExitsAsync(int idcompany, TblUser khachhang)
		{
			var _khachhang = _context.TblUsers
				.Where(b => b.Idcompany == idcompany && b.Dienthoai == khachhang.Dienthoai)
				.FirstOrDefault();
			if (_khachhang != null)
			{
				return _khachhang;
			}
			else
				return new TblUser();
		}


		public async Task<TblKhachHangGhiChu> InsertGhiChuKhachHangAsync(TblKhachHangGhiChu khachhangghichu)
		{
			await _context.TblKhachHangGhiChus.AddAsync(khachhangghichu);
			int user_insert = await _context.SaveChangesAsync();
			return await Task.FromResult(khachhangghichu);
		}

		public async Task<List<TblKhachHangGhiChu>> GetAllGhiChuKhachHangAsync(int idkhachhang)
		{
			return await _context.TblKhachHangGhiChus.Where(b => b.Idkhachhang == idkhachhang).ToListAsync();
		}

        public async Task<List<TblKhachHangGhiChu>> GetAllGhiChuKhachHangAsync()
        {
            return await _context.TblKhachHangGhiChus.OrderByDescending(b => b.Id).ToListAsync();
        }

        public async Task<TblKhachHangGhiChu> DeleteGhiChuKhachHangAsync(int id)
		{
			TblKhachHangGhiChu _ghichu = await _context.TblKhachHangGhiChus.FindAsync(id);
			if (_ghichu != null)
			{
				_context.TblKhachHangGhiChus.Remove(_ghichu);
				await _context.SaveChangesAsync();
			}
			return _ghichu;
		}



        public async Task<List<TblLoaiChamSocKh>> GetAllLoaiChamSocKhachHangAsync()
        {
            return await _context.TblLoaiChamSocKhs.ToListAsync();
        }

     


    }
}
