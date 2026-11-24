using MudBlazor;

namespace MTL.APP.Services;

public static class AppThemeCatalog
{
	public const string CrmRed = "crm-red";
	public const string CrmBlue = "crm-blue";
	public const string Forest = "forest";
	public const string Midnight = "midnight";
	public const string Sunset = "sunset";
	public const string Ocean = "ocean";
	public const string RoyalGold = "royal-gold";
	public const string Indigo = "indigo";
	public const string Rose = "rose";
	public const string Slate = "slate";

	private static readonly AppThemeOption[] _options =
	{
		new(CrmRed, "CRM đỏ (mặc định)", false, "#D32F2F"),
		new(CrmBlue, "Xanh báo cáo", false, "#1565C0"),
		new(Forest, "Xanh lá", false, "#2E7D32"),
		new(Midnight, "Tối (Midnight)", true, "#311B92"),
		new(Sunset, "Hoàng hôn cam", false, "#BF360C"),
		new(Ocean, "Đại dương", false, "#00695C"),
		new(RoyalGold, "Hoàng gia vàng", false, "#F57F17"),
		new(Indigo, "Chàm tím", false, "#3949AB"),
		new(Rose, "Hồng đào", false, "#C2185B"),
		new(Slate, "Xám than", false, "#455A64"),
	};

	public static IReadOnlyList<AppThemeOption> Options => _options;

	public static bool IsValid(string? id) =>
		id is not null && _options.Any(o => o.Id == id);

	public static string Normalize(string? id) =>
		IsValid(id) ? id! : CrmRed;

	public static bool IsDark(string id) =>
		_options.FirstOrDefault(o => o.Id == id)?.DarkMode ?? false;

	public static MudTheme GetMudTheme(string id) => Normalize(id) switch
	{
		CrmBlue => BuildBlue(),
		Forest => BuildForest(),
		Midnight => BuildMidnight(),
		Sunset => BuildSunset(),
		Ocean => BuildOcean(),
		RoyalGold => BuildRoyalGold(),
		Indigo => BuildIndigo(),
		Rose => BuildRose(),
		Slate => BuildSlate(),
		_ => BuildRed(),
	};

	private static MudTheme BuildRed() => new()
	{
		PaletteLight = new PaletteLight
		{
			Primary = "#D32F2F",
			PrimaryDarken = "#B71C1C",
			PrimaryLighten = "#EF5350",
			Secondary = "#212121",
			SecondaryDarken = "#000000",
			SecondaryLighten = "#424242",
			Tertiary = "#FF5722",
			Info = "#0288D1",
			Success = "#388E3C",
			Warning = "#F57C00",
			Error = "#D32F2F",
			Background = "#FAFAFA",
			Surface = "#FFFFFF",
			AppbarBackground = "#D32F2F",
			AppbarText = "#FFFFFF",
			DrawerBackground = "#FFFFFF",
			DrawerText = "#212121",
			DrawerIcon = "#D32F2F",
			TextPrimary = "#212121",
			TextSecondary = "#757575",
			Divider = "#E0E0E0",
			TableHover = "#FFEBEE",
			TableStriped = "#FFF3E0",
		},
		PaletteDark = new PaletteDark
		{
			Primary = "#EF5350",
			PrimaryDarken = "#D32F2F",
			PrimaryLighten = "#FFCDD2",
			Background = "#121212",
			Surface = "#1E1E1E",
			AppbarBackground = "#1E1E1E",
			AppbarText = "#FFFFFF",
		},
		LayoutProperties = new LayoutProperties { AppbarHeight = "64px" },
		Typography = SharedTypography(),
	};

	private static MudTheme BuildBlue() => new()
	{
		PaletteLight = new PaletteLight
		{
			Primary = "#1976D2",
			PrimaryDarken = "#1565C0",
			PrimaryLighten = "#42A5F5",
			Secondary = "#37474F",
			SecondaryDarken = "#263238",
			SecondaryLighten = "#546E7A",
			Tertiary = "#00897B",
			Info = "#0288D1",
			Success = "#2E7D32",
			Warning = "#F57C00",
			Error = "#C62828",
			Background = "#F5F9FF",
			Surface = "#FFFFFF",
			AppbarBackground = "#1565C0",
			AppbarText = "#FFFFFF",
			DrawerBackground = "#FFFFFF",
			DrawerText = "#212121",
			DrawerIcon = "#1976D2",
			TextPrimary = "#212121",
			TextSecondary = "#546E7A",
			Divider = "#E3F2FD",
			TableHover = "#E3F2FD",
			TableStriped = "#FFF8E1",
		},
		PaletteDark = new PaletteDark
		{
			Primary = "#42A5F5",
			Background = "#121212",
			Surface = "#1E1E1E",
			AppbarBackground = "#0D47A1",
			AppbarText = "#FFFFFF",
		},
		LayoutProperties = new LayoutProperties { AppbarHeight = "64px" },
		Typography = SharedTypography(),
	};

	private static MudTheme BuildForest() => new()
	{
		PaletteLight = new PaletteLight
		{
			Primary = "#2E7D32",
			PrimaryDarken = "#1B5E20",
			PrimaryLighten = "#66BB6A",
			Secondary = "#33691E",
			SecondaryDarken = "#1B5E20",
			SecondaryLighten = "#558B2F",
			Tertiary = "#00897B",
			Info = "#0277BD",
			Success = "#2E7D32",
			Warning = "#EF6C00",
			Error = "#C62828",
			Background = "#F1F8F1",
			Surface = "#FFFFFF",
			AppbarBackground = "#2E7D32",
			AppbarText = "#FFFFFF",
			DrawerBackground = "#FFFFFF",
			DrawerText = "#212121",
			DrawerIcon = "#2E7D32",
			TextPrimary = "#1B2E1B",
			TextSecondary = "#5D6E5D",
			Divider = "#E0F2E0",
			TableHover = "#E8F5E9",
			TableStriped = "#FFFDE7",
		},
		PaletteDark = new PaletteDark
		{
			Primary = "#66BB6A",
			Background = "#121212",
			Surface = "#1E1E1E",
			AppbarBackground = "#1B5E20",
			AppbarText = "#FFFFFF",
		},
		LayoutProperties = new LayoutProperties { AppbarHeight = "64px" },
		Typography = SharedTypography(),
	};

	private static MudTheme BuildMidnight() => new()
	{
		PaletteLight = new PaletteLight
		{
			Primary = "#7C4DFF",
			PrimaryDarken = "#5E35B1",
			PrimaryLighten = "#B388FF",
			Secondary = "#90A4AE",
			Background = "#FAFAFA",
			Surface = "#FFFFFF",
			AppbarBackground = "#311B92",
			AppbarText = "#FFFFFF",
			DrawerBackground = "#FFFFFF",
			DrawerText = "#212121",
			DrawerIcon = "#7C4DFF",
			TextPrimary = "#212121",
			TextSecondary = "#757575",
			Divider = "#E0E0E0",
			TableHover = "#EDE7F6",
			TableStriped = "#F3E5F5",
		},
		PaletteDark = new PaletteDark
		{
			Primary = "#B388FF",
			PrimaryDarken = "#7C4DFF",
			PrimaryLighten = "#E1BEE7",
			Secondary = "#90A4AE",
			Background = "#0D1117",
			Surface = "#161B22",
			AppbarBackground = "#161B22",
			AppbarText = "#E6EDF3",
			DrawerBackground = "#161B22",
			DrawerText = "#E6EDF3",
			DrawerIcon = "#B388FF",
			TextPrimary = "#E6EDF3",
			TextSecondary = "#8B949E",
			Divider = "#30363D",
			TableHover = "#21262D",
			TableStriped = "#1C2128",
		},
		LayoutProperties = new LayoutProperties { AppbarHeight = "64px" },
		Typography = SharedTypography(),
	};

	private static Typography SharedTypography() => new()
	{
		H1 = new H1 { FontWeight = 700, FontSize = "2.25rem" },
		H2 = new H2 { FontWeight = 700, FontSize = "1.875rem" },
		H3 = new H3 { FontWeight = 600, FontSize = "1.5rem" },
		H4 = new H4 { FontWeight = 600, FontSize = "1.25rem" },
		H5 = new H5 { FontWeight = 600, FontSize = "1.1rem" },
		H6 = new H6 { FontWeight = 600, FontSize = "1rem" },
		Button = new Button { FontWeight = 600, TextTransform = "none" },
		Subtitle1 = new Subtitle1 { FontWeight = 500 },
		Subtitle2 = new Subtitle2 { FontWeight = 500 },
	};

	private static MudTheme BuildSunset() => new()
	{
		PaletteLight = new PaletteLight
		{
			Primary = "#E65100",
			PrimaryDarken = "#BF360C",
			PrimaryLighten = "#FF8A65",
			Secondary = "#4E342E",
			SecondaryDarken = "#3E2723",
			SecondaryLighten = "#795548",
			Tertiary = "#F9A825",
			Info = "#0277BD",
			Success = "#558B2F",
			Warning = "#F57C00",
			Error = "#C62828",
			Background = "#FFF8F4",
			Surface = "#FFFFFF",
			AppbarBackground = "#BF360C",
			AppbarText = "#FFFFFF",
			DrawerBackground = "#FFFFFF",
			DrawerText = "#3E2723",
			DrawerIcon = "#E65100",
			TextPrimary = "#3E2723",
			TextSecondary = "#6D4C41",
			Divider = "#FFCCBC",
			TableHover = "#FFF3E0",
			TableStriped = "#FBE9E7",
		},
		PaletteDark = new PaletteDark
		{
			Primary = "#FF8A65",
			PrimaryDarken = "#E65100",
			Background = "#121212",
			Surface = "#1E1E1E",
			AppbarBackground = "#3E2723",
			AppbarText = "#FFCCBC",
		},
		LayoutProperties = new LayoutProperties { AppbarHeight = "64px" },
		Typography = SharedTypography(),
	};

	private static MudTheme BuildOcean() => new()
	{
		PaletteLight = new PaletteLight
		{
			Primary = "#00695C",
			PrimaryDarken = "#004D40",
			PrimaryLighten = "#26A69A",
			Secondary = "#006064",
			SecondaryDarken = "#004D40",
			SecondaryLighten = "#00838F",
			Tertiary = "#00897B",
			Info = "#0277BD",
			Success = "#2E7D32",
			Warning = "#F57C00",
			Error = "#C62828",
			Background = "#F0FAFA",
			Surface = "#FFFFFF",
			AppbarBackground = "#00695C",
			AppbarText = "#FFFFFF",
			DrawerBackground = "#FFFFFF",
			DrawerText = "#004D40",
			DrawerIcon = "#00897B",
			TextPrimary = "#004D40",
			TextSecondary = "#00695C",
			Divider = "#B2DFDB",
			TableHover = "#E0F2F1",
			TableStriped = "#E0F7FA",
		},
		PaletteDark = new PaletteDark
		{
			Primary = "#26A69A",
			PrimaryDarken = "#00897B",
			Background = "#121212",
			Surface = "#1E1E1E",
			AppbarBackground = "#004D40",
			AppbarText = "#B2DFDB",
		},
		LayoutProperties = new LayoutProperties { AppbarHeight = "64px" },
		Typography = SharedTypography(),
	};

	private static MudTheme BuildRoyalGold() => new()
	{
		PaletteLight = new PaletteLight
		{
			Primary = "#F57F17",
			PrimaryDarken = "#E65100",
			PrimaryLighten = "#FFD54F",
			Secondary = "#4E342E",
			SecondaryDarken = "#3E2723",
			SecondaryLighten = "#795548",
			Tertiary = "#F9A825",
			Info = "#0277BD",
			Success = "#558B2F",
			Warning = "#F9A825",
			Error = "#C62828",
			Background = "#FFFCF5",
			Surface = "#FFFFFF",
			AppbarBackground = "#F57F17",
			AppbarText = "#FFFFFF",
			DrawerBackground = "#FFFFFF",
			DrawerText = "#4E342E",
			DrawerIcon = "#F57F17",
			TextPrimary = "#4E342E",
			TextSecondary = "#795548",
			Divider = "#FFE082",
			TableHover = "#FFF8E1",
			TableStriped = "#FFFDE7",
		},
		PaletteDark = new PaletteDark
		{
			Primary = "#FFD54F",
			PrimaryDarken = "#F57F17",
			Background = "#121212",
			Surface = "#1E1E1E",
			AppbarBackground = "#4E342E",
			AppbarText = "#FFE082",
		},
		LayoutProperties = new LayoutProperties { AppbarHeight = "64px" },
		Typography = SharedTypography(),
	};

	private static MudTheme BuildIndigo() => new()
	{
		PaletteLight = new PaletteLight
		{
			Primary = "#5C6BC0",
			PrimaryDarken = "#3949AB",
			PrimaryLighten = "#9FA8DA",
			Secondary = "#37474F",
			SecondaryDarken = "#263238",
			SecondaryLighten = "#546E7A",
			Tertiary = "#7E57C2",
			Info = "#0288D1",
			Success = "#2E7D32",
			Warning = "#F57C00",
			Error = "#C62828",
			Background = "#F5F6FF",
			Surface = "#FFFFFF",
			AppbarBackground = "#3949AB",
			AppbarText = "#FFFFFF",
			DrawerBackground = "#FFFFFF",
			DrawerText = "#212121",
			DrawerIcon = "#5C6BC0",
			TextPrimary = "#212121",
			TextSecondary = "#5C6BC0",
			Divider = "#E8EAF6",
			TableHover = "#E8EAF6",
			TableStriped = "#F3E5F5",
		},
		PaletteDark = new PaletteDark
		{
			Primary = "#9FA8DA",
			PrimaryDarken = "#5C6BC0",
			Background = "#121212",
			Surface = "#1E1E1E",
			AppbarBackground = "#1A237E",
			AppbarText = "#FFFFFF",
		},
		LayoutProperties = new LayoutProperties { AppbarHeight = "64px" },
		Typography = SharedTypography(),
	};

	private static MudTheme BuildRose() => new()
	{
		PaletteLight = new PaletteLight
		{
			Primary = "#EC407A",
			PrimaryDarken = "#C2185B",
			PrimaryLighten = "#F48FB1",
			Secondary = "#4E342E",
			SecondaryDarken = "#3E2723",
			SecondaryLighten = "#795548",
			Tertiary = "#AB47BC",
			Info = "#0288D1",
			Success = "#2E7D32",
			Warning = "#F9A825",
			Error = "#B71C1C",
			Background = "#FFF5F8",
			Surface = "#FFFFFF",
			AppbarBackground = "#C2185B",
			AppbarText = "#FFFFFF",
			DrawerBackground = "#FFFFFF",
			DrawerText = "#3E2723",
			DrawerIcon = "#EC407A",
			TextPrimary = "#3E2723",
			TextSecondary = "#AD1457",
			Divider = "#F8BBD0",
			TableHover = "#FCE4EC",
			TableStriped = "#F3E5F5",
		},
		PaletteDark = new PaletteDark
		{
			Primary = "#F48FB1",
			PrimaryDarken = "#EC407A",
			Background = "#121212",
			Surface = "#1E1E1E",
			AppbarBackground = "#880E4F",
			AppbarText = "#FCE4EC",
		},
		LayoutProperties = new LayoutProperties { AppbarHeight = "64px" },
		Typography = SharedTypography(),
	};

	private static MudTheme BuildSlate() => new()
	{
		PaletteLight = new PaletteLight
		{
			Primary = "#607D8B",
			PrimaryDarken = "#455A64",
			PrimaryLighten = "#90A4AE",
			Secondary = "#37474F",
			SecondaryDarken = "#263238",
			SecondaryLighten = "#546E7A",
			Tertiary = "#00897B",
			Info = "#0277BD",
			Success = "#2E7D32",
			Warning = "#F57C00",
			Error = "#C62828",
			Background = "#F5F7F8",
			Surface = "#FFFFFF",
			AppbarBackground = "#455A64",
			AppbarText = "#FFFFFF",
			DrawerBackground = "#FFFFFF",
			DrawerText = "#263238",
			DrawerIcon = "#607D8B",
			TextPrimary = "#263238",
			TextSecondary = "#546E7A",
			Divider = "#CFD8DC",
			TableHover = "#ECEFF1",
			TableStriped = "#ECEFF1",
		},
		PaletteDark = new PaletteDark
		{
			Primary = "#90A4AE",
			PrimaryDarken = "#607D8B",
			Background = "#121212",
			Surface = "#1E1E1E",
			AppbarBackground = "#37474F",
			AppbarText = "#ECEFF1",
		},
		LayoutProperties = new LayoutProperties { AppbarHeight = "64px" },
		Typography = SharedTypography(),
	};
}

public sealed record AppThemeOption(string Id, string Label, bool DarkMode, string SwatchHex);
