namespace MTL.APP.IServices
{
	public interface IWindowSizeInterop
	{
		Task<WindowSize> GetWindowSize();
	}
	public class WindowSize
	{
		public int Width { get; set; }
		public int Height { get; set; }
	}
}
