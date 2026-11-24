namespace MTL.APP.WordPressRestAPI
{

	public class Posts : List<Post> { }
	public class Post
	{
		public string id { get; set; }
		public string status { get; set; }
		public string link { get; set; }
		public string type { get; set; }
		public string slug { get; set; }
		public DateTime date { get; set; }
		public DateTime date_gmt { get; set; }
		public Title title { get; set; }
		public Content content { get; set; }
		public int featured_media { get; set; }

	}
	public class Content
	{
		//public string protected { get; set; }
		public string rendered { get; set; }
	}
	public class Title
	{
		public string raw { get; set; }
		public string rendered { get; set; }
	}
}
