using Blazored.LocalStorage;
using Blazored.Toast;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.EntityFrameworkCore;
using MTL.APP.Authentication;
using MTL.APP.Hubs;
using MTL.APP.IServices;
using MTL.APP.Models;
using MTL.APP.Services;
using MTL.APP.Utils;
using MTL.DEV.Handlers;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

void ConfigureMtlDb(DbContextOptionsBuilder options)
{
	options.UseSqlServer(
		builder.Configuration.GetConnectionString("MTLDB"),
		o => o.CommandTimeout(60).ExecutionStrategy(x => new Microsoft.EntityFrameworkCore.Storage.NonRetryingExecutionStrategy(x)));
}

// Chỉ dùng AddDbContextFactory: EF Core 9 đăng ký luôn MTLContext scoped cho inject trực tiếp;
// gọi thêm AddDbContext cùng lúc gây lỗi "Cannot resolve scoped service IDbContextOptionsConfiguration from root provider".
builder.Services.AddDbContextFactory<MTLContext>(ConfigureMtlDb);
builder.Services.AddAuthenticationCore();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor().AddCircuitOptions(option => { option.DetailedErrors = true; });
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddTransient<ValidateHeaderHandler>();
builder.Services.AddHttpClient<APIWooService>().AddHttpMessageHandler<ValidateHeaderHandler>();
builder.Services.AddHttpClient<WordPressRESTAPI>().AddHttpMessageHandler<ValidateHeaderHandler>();

builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<HoaDonService>();
builder.Services.AddScoped<HopDongService>();
builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<HangHoaService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<KhachHangService>();
builder.Services.AddScoped<FacebookMessageService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<ZnsVeSinhCampaignService>();
builder.Services.AddScoped<ThuHangHoaService>();
builder.Services.AddScoped<CVJPService>();
builder.Services.AddScoped<Configs>();
builder.Services.AddScoped<IZaloMessengerService>(sp =>
    new ZaloMessengerService(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ILogger<ZaloMessengerService>>(),
        sp));
builder.Services.AddHttpClient("N8N");
builder.Services.AddScoped<IZaloOaService, ZaloOaService>();

builder.Services.AddSignalR();
builder.Services.AddScoped<AppThemeService>();
builder.Services.AddScoped<GymService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IWindowSizeInterop, WindowSizeInterop>();
builder.Services.AddSingleton<ICameraService, CameraService>();
builder.Services.AddScoped<IImageCacheService, ImageCacheService>();
builder.Services.AddBlazoredToast();

builder.Services.AddMudServices(config =>
{
	config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
	config.SnackbarConfiguration.PreventDuplicates = false;
	config.SnackbarConfiguration.NewestOnTop = false;
	config.SnackbarConfiguration.ShowCloseIcon = true;
	config.SnackbarConfiguration.VisibleStateDuration = 3000;
	config.SnackbarConfiguration.HideTransitionDuration = 500;
	config.SnackbarConfiguration.ShowTransitionDuration = 500;
	config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

builder.Services.AddMudExtensions();

builder.Services.AddMudServicesWithExtensions(c =>
{
	c.WithDefaultDialogOptions(ex =>
	{
		ex.MaximizeButton = true;
		ex.CloseButton = true;
		ex.Position = DialogPosition.Center;
		ex.Animations = new[] {
			MudBlazor.Extensions.Options.AnimationType.JackInTheBox,
			MudBlazor.Extensions.Options.AnimationType.Back
		};
		ex.FullHeight = true;
		ex.Resizeable = true;
		ex.MaxWidth = MaxWidth.Medium;
		ex.FullWidth = true;
		ex.DragMode = MudDialogDragMode.Simple;
		ex.CloseOnEscapeKey = true;
		ex.FullScreen = true;
	});
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapBlazorHub();
app.MapHub<MTL.APP.Hubs.ZaloHub>("/hubs/zalo");
app.MapFallbackToPage("/_Host");
app.MapControllers();
app.Run();
