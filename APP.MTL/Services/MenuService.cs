using MTL.APP.Models;

namespace MTL.APP.Services
{
	public interface IMenuService
	{
		Task<List<MTL_GetAllNhomHangResult>> GetMenuItemsAsync(int idLoaiHinhKinhDoanh, int idcompany);
		List<MTL_GetAllNhomHangResult> ListmenuItems();
	}
	public class MenuService : IMenuService
	{
		private readonly HangHoaService _hangHoaService;
		public List<MTL_GetAllNhomHangResult> menuItems { get; set; }
		public MenuService(HangHoaService hangHoaService)
		{
			_hangHoaService = hangHoaService;
		}


		public List<MTL_GetAllNhomHangResult> ListmenuItems()
		{
			return menuItems;
		}
		public async Task<List<MTL_GetAllNhomHangResult>> GetMenuItemsAsync(int idLoaiHinhKinhDoanh, int idcompany)
		{
			menuItems = await _hangHoaService.MTL_GetAllNhomHangAsync(idLoaiHinhKinhDoanh, idcompany);
			return menuItems;
		}
	}

}
