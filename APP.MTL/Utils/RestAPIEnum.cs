namespace MTL.APP.Utils
{
	public class RestAPIEnum
	{
		public class post_status
		{
			public static List<String> data
				= new List<string> { "publish", "future", "draft", "private" };

			private string status = "";
			public string Status
			{
				get { return status; }
				set { status = value; }
			}
			public post_status(string status)
			{
				this.Status = status;
			}

			public const string
				publish = "publish",
				future = "future",
				draft = "draft",
				pending = "pending",
				private1 = "private";
		}
		public static List<post_status> GetListPostStatus()
		{
			List<post_status> list = new List<post_status>();
			list.Add(new post_status("publish"));
			list.Add(new post_status("future"));
			list.Add(new post_status("draft"));
			list.Add(new post_status("pending"));
			list.Add(new post_status("private"));
			return list;
		}



	}
}
