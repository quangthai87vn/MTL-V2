using Microsoft.EntityFrameworkCore;
using MTL.APP.Models;

namespace MTL.APP.Services
{
	public class HopDongService
	{
		private readonly MTLContext _context;
		public HopDongService(MTLContext appDBContext)
		{
			_context = appDBContext;
		}
		public async Task<List<HopDong>> GetAllHopDongAsync()
		{
			return await _context.HopDongs
				.OrderBy(b => b.Ngaytaiky)
				.ToListAsync();
		}
		public async Task<HopDong> GetHopDongAsync(int? id)
		{
			var hopdong = await _context.HopDongs.FindAsync(id);
			if (hopdong == null)
			{
				return null;
			}
			else
				return hopdong;
		}
		public async Task<HopDong> CreateHopDongAsync(HopDong hopdong)
		{
			_context.HopDongs.Add(hopdong);
			await _context.SaveChangesAsync();
			return hopdong;
		}
		public async Task<HopDong> UpdateHopDongAsync(HopDong hopdong)
		{
			int result = 0;
			var _hopdong = _context.HopDongs.FirstOrDefault(hd => hd.Id == hopdong.Id);
			if (_hopdong != null)
			{
				//sửa
				_hopdong.Noidunghd = hopdong.Noidunghd;
				_hopdong.Tenkhachhang = hopdong.Tenkhachhang;
				_hopdong.Sdtkhachhang = hopdong.Sdtkhachhang;
				_hopdong.Diachikhachhang = hopdong.Diachikhachhang;
				_hopdong.Ghichu = hopdong.Ghichu;
				_hopdong.Ngayky = hopdong.Ngayky;
				_hopdong.Ngaytaiky = hopdong.Ngaytaiky;
				_hopdong.Giatrihd = hopdong.Giatrihd;
				_hopdong.Tongthanhtoan = hopdong.Tongthanhtoan;
				result = await _context.SaveChangesAsync();

			}
			return _hopdong;
		}



	}
}
