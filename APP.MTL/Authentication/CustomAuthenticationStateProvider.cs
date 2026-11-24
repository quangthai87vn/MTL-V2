using System.Security.Claims;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
namespace MTL.APP.Authentication
{
	public class CustomAuthenticationStateProvider : AuthenticationStateProvider
	{
		private ProtectedLocalStorage _browserStorage;
		public ILocalStorageService _localStorageService { get; }

		private ClaimsPrincipal _anonymous = new ClaimsPrincipal(new ClaimsIdentity());
		public CustomAuthenticationStateProvider(ProtectedLocalStorage browserStorage, ILocalStorageService iLocalStorageService)
		{
			_browserStorage = browserStorage;
			_localStorageService = iLocalStorageService;
		}
		public override async Task<AuthenticationState> GetAuthenticationStateAsync()
		{
			try
			{
				//var userSession = await _localStorageService.GetItemAsync<UserSession>("UserSession");
				var userSessionStorageResult = await _browserStorage.GetAsync<UserSession>("UserSession");
				var userSession = userSessionStorageResult.Success ? userSessionStorageResult.Value : null;
				if (userSession == null)
				{
					return await Task.FromResult(new AuthenticationState(_anonymous));
				}
				var claimPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
					{
						new Claim(ClaimTypes.Email, userSession.Email),
						new Claim(ClaimTypes.Name, userSession.FullName),
						new Claim(ClaimTypes.Role, userSession.Role)
					}, "CustomAuth"));
				return await Task.FromResult(new AuthenticationState(claimPrincipal));

			}
			catch
			{
				return await Task.FromResult(new AuthenticationState(_anonymous));
			}
		}
		public async Task UpdateAuthenticationState(UserSession userSession)
		{
			ClaimsPrincipal claimsPrincipal;
			if (userSession != null)
			{
				await _browserStorage.SetAsync("UserSession", userSession);
				claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
				{
					new Claim(ClaimTypes.Email, userSession.Email),
					new Claim(ClaimTypes.Name, userSession.FullName),
					new Claim(ClaimTypes.Role, userSession.Role)
				}, "CustomAuth"));
			}
			else
			{
				await _browserStorage.DeleteAsync("UserSession");
				claimsPrincipal = _anonymous;
			}
			NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
		}

		public async Task StartUserSession(UserSession userSession)
		{
			// await _browserStorage.SetAsync("UserSession", userSession);

			var identity = new ClaimsIdentity(new[]
			{
				new Claim(ClaimTypes.Email, userSession.Email),
				new Claim(ClaimTypes.Name, userSession.FullName),
				new Claim(ClaimTypes.Role, userSession.Role)
			}, "CustomAuth");
			var userAccount = new ClaimsPrincipal(identity);
			NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(userAccount)));
		}

	}
}
