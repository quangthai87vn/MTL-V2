using MTL.APP.Models;

namespace MTL.APP.Authentication
{
	public class UserSession
	{
		public int UserId { get; set; }
		public string Email { get; set; }
		public int idRole { get; set; }
		public string Role { get; set; }
		public bool isLogin { get; set; }
		public string FullName { get; set; }
		public string CompanyName { get; set; }
		public int idcompany { get; set; }
		public Company company { get; set; }
		public User user { get; set; }
	}
}
