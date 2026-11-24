using Microsoft.EntityFrameworkCore;
using MTL.APP.Models;

namespace MTL.APP.Services
{
	public class HoaDonService
	{
		private readonly MTLContext _context;
		public HoaDonService(MTLContext appDBContext)
		{
			_context = appDBContext;
		}
		public async Task<List<TblHoaDonBanHang>> GetAllHoaDonBanHangAsync()
		{
			return await _context.TblHoaDonBanHangs.ToListAsync();
		}

		public async Task<IEnumerable<MTL_GetAllHoaDonCuResult>> GetAllHoaDonCuAsync()
		{
			return await _context.MTL_GetAllHoaDonCu(true)
				.ToListAsync();
		}
		public async Task<IEnumerable<TblHoaDonChiTiet>> GetAllHoaDonChiTietCuAsync(int IdhoaDon)
		{
			return await _context.TblHoaDonChiTiets
					.Where(b => b.IdhoaDon == IdhoaDon)
					.ToListAsync();
		}








	}
}
