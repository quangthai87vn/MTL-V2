using Microsoft.JSInterop;
using MTL.APP.IServices;

namespace MTL.APP.Services
{
	public class WindowSizeInterop : IWindowSizeInterop
	{
		private readonly IJSRuntime _jsRuntime;

		public WindowSizeInterop(IJSRuntime jsRuntime)
		{
			_jsRuntime = jsRuntime;
		}

		public async Task<WindowSize> GetWindowSize()
		{
			return await _jsRuntime.InvokeAsync<WindowSize>("windowSizeService.getWindowSize");
		}
	}
}
