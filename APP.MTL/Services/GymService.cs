using Microsoft.EntityFrameworkCore;
using MTL.APP.Models;  namespace MTL.APP.Services {
	public class GymService
	{
		private readonly MTLContext _context;
		public GymService(MTLContext context)
		{
			_context = context;
		}
		public async Task<int> GymCheckInCustomerAsync(string MaKH, int idcompany)
		{
			int result = 0;
			int kq = await _context.Procedures.GymCheckInCustomerAsync(MaKH, idcompany);
			result = await _context.SaveChangesAsync();
			return kq;
		}
		public async Task<int> GymCheckOutCustomerAsync(string MaKH, int idcompany)
		{
			int result = 0;
			int kq = await _context.Procedures.GymCheckOutCustomerAsync(MaKH, idcompany);
			result = await _context.SaveChangesAsync();
			return kq;
		}

		public async Task<int> UpdateGymSessionAsync(int SessionID, DateTime CheckInTime, DateTime CheckOutTime)
		{
			int result = 0;
			int kq = await _context.Procedures.UpdateGymSessionAsync(SessionID, CheckInTime, CheckOutTime);
			result = await _context.SaveChangesAsync();
			return kq;
		}


		public async Task<List<GymSession>> GetAllGymSessionAsync(int idcompany)
		{
			return await _context.GymSessions
				//.Where(b => b.Idcompany == idcompany)
				.OrderByDescending(b => b.SessionId)
				.ToListAsync();
		}

		public async Task<List<GymSession>> GetAllGymSessionAsync(int idcompany, int CustomerID)
		{
			return await _context.GymSessions
				//.Where(b => b.Idcompany == idcompany)
				.Where(b => b.CustomerId == CustomerID)
				.OrderByDescending(b => b.SessionId)
				.ToListAsync();
		}

		public async Task<List<GymGetAllUsersWorkoutTimesResult>> GymGetAllUsersWorkoutTimes(int idcompany)
		{
			return await _context.GymGetAllUsersWorkoutTimes(idcompany)
				.ToListAsync();
		}
		public async Task<GymSession> DeleteGymSessionAsync(GymSession _GymSession)
		{
			GymSession _GYM = await _context.GymSessions.FindAsync(_GymSession.SessionId);
			if (_GYM != null)
			{
				_context.GymSessions.Remove(_GYM);
				await _context.SaveChangesAsync();
			}
			return _GYM;
		}
	} } 