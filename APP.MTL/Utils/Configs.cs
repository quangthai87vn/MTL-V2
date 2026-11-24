using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using MTL.APP.Authentication;

namespace MTL.APP.Utils
{
	public class Configs
	{
		public ProtectedLocalStorage protectedLocalStorage;
		public Configs(ProtectedLocalStorage browserStorage)
		{
			protectedLocalStorage = browserStorage;

		}
		public static string Encrypt(string password)
		{
			var provider = MD5.Create();
			string salt = "S0m3R@nd0mSalt";
			byte[] bytes = provider.ComputeHash(Encoding.UTF32.GetBytes(salt + password));
			return BitConverter.ToString(bytes).Replace("-", "").ToLower();
		}

		public async Task<UserSession> LoadAllConfig()
		{
			UserSession userSession = new UserSession();
			var UserSessionStorageResult = await protectedLocalStorage.GetAsync<UserSession>("UserSession");
			var _UserSession = UserSessionStorageResult.Success ? UserSessionStorageResult.Value : null;
			if (_UserSession != null)
				userSession = (UserSession)_UserSession;
			return userSession;
		}




	}
}
