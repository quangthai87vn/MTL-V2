using MTL.APP.Models;

namespace MTL.APP
{
	public class UserPopupInfo
	{
		public bool isNew = true;
		public string HeaderText = "";
		public bool PopupVisible { get; set; }
		public User UserInfo { get; set; } = new User();
	}
}
