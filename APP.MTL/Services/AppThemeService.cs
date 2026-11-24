using Blazored.LocalStorage;
using MudBlazor;

namespace MTL.APP.Services;

public class AppThemeService
{
	public const string StorageKey = "mtl.ui.theme";

	private readonly ILocalStorageService _localStorage;
	private string _themeId = AppThemeCatalog.CrmRed;
	private bool _initialized;

	public AppThemeService(ILocalStorageService localStorage) =>
		_localStorage = localStorage;

	public string ThemeId => _themeId;

	public MudTheme CurrentMudTheme => AppThemeCatalog.GetMudTheme(_themeId);

	public bool IsDarkMode => AppThemeCatalog.IsDark(_themeId);

	public event Action? ThemeChanged;

	public async Task InitializeAsync()
	{
		if (_initialized) return;
		try
		{
			var stored = await _localStorage.GetItemAsync<string>(StorageKey);
			if (AppThemeCatalog.IsValid(stored))
				_themeId = stored!;
		}
		catch
		{
			/* SSR / prerender */
		}

		_initialized = true;
	}

	public async Task SetThemeAsync(string id)
	{
		var next = AppThemeCatalog.Normalize(id);
		if (_themeId == next) return;
		_themeId = next;
		try
		{
			await _localStorage.SetItemAsync(StorageKey, _themeId);
		}
		catch
		{
			/* ignore */
		}

		ThemeChanged?.Invoke();
	}
}
