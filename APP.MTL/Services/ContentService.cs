using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.APP.Models;




namespace MTL.APP.Services
{
	public class ContentService
	{
		private readonly MTLContext _context;
		public ContentService(MTLContext appDBContext)
		{
			_context = appDBContext;
		}
		public async Task<List<TimeSheetBaiViet>> GetAllNoiDungAsync()
		{
			return await _context.TimeSheetBaiViets
				.OrderByDescending(b => b.Ngaylenbai)
				.ToListAsync();
		}

		public async Task<List<TimeSheetDuAn>> GetAllDuAnAsync()
		{
			return await _context.TimeSheetDuAns
				//.OrderBy(b => b.Lastupdate)
				.ToListAsync();
		}
		public async Task<TimeSheetBaiViet> CreateTimeSheetBaiVietAsync(TimeSheetBaiViet timeSheetBaiViet)
		{
			_context.TimeSheetBaiViets.Add(timeSheetBaiViet);
			await _context.SaveChangesAsync();
			return timeSheetBaiViet;
		}
		public async Task<TimeSheetBaiViet> UpdateTimeSheetBaiVietAsync(TimeSheetBaiViet timeSheetBaiViet)
		{
			int result = 0;
			TimeSheetBaiViet thuonghieu_up = new TimeSheetBaiViet();
			var _baiviet = _context.TimeSheetBaiViets.FirstOrDefault(hd => hd.Id == timeSheetBaiViet.Id);
			if (_baiviet != null)
			{
				//sửa

				_baiviet.Link = timeSheetBaiViet.Link;
				_baiviet.Tenbaidang = timeSheetBaiViet.Tenbaidang;
				_baiviet.Idduan = timeSheetBaiViet.Idduan;
				_baiviet.Lastupdate = DateTime.Now;
				_baiviet.Beginupdate = timeSheetBaiViet.Beginupdate;
				_baiviet.Noidung = timeSheetBaiViet.Noidung;
				_baiviet.Facebook = timeSheetBaiViet.Facebook;
				_baiviet.Idusertask = timeSheetBaiViet.Idusertask;
				_baiviet.Iduserupdate = timeSheetBaiViet.Iduserupdate;
				_baiviet.Keytag = timeSheetBaiViet.Keytag;
				result = await _context.SaveChangesAsync();

			}
			return _baiviet;
		}
		public async Task<TimeSheetBaiViet> GetTimeSheetBaiVietAsync(int? id)
		{
			var baiviet = await _context.TimeSheetBaiViets.FindAsync(id);
			if (baiviet == null)
			{
				return null;
			}
			else
				return baiviet;
		}

		public async Task<ActionResult<TimeSheetBaiViet>> DeleteTimeSheetBaiVietAsync(int id)
		{
			TimeSheetBaiViet baiviet = await _context.TimeSheetBaiViets.FindAsync(id);
			if (baiviet != null)
			{
				_context.TimeSheetBaiViets.Remove(baiviet);
				await _context.SaveChangesAsync();
			}
			return baiviet;
		}




        public async Task<List<TblZnztemplate>> GetZnzTemplateAsync(int idcompany)
        {
            return await _context.TblZnztemplates
                .Where(b => b.Idcompany == idcompany)
                // .OrderByDescending(b => b.Ngaylenbai)
                .ToListAsync();
        }


    }
}
