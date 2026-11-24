namespace MTL.APP.WooRestAPI
{
    public class Categories : List<Category> { }
    public class Category
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string slug { get; set; }
        public int count { get; set; }
    }


    public class images : List<image> { }
    public class image
    {
        public int id { get; set; }
        public string src { get; set; }
        public string name { get; set; }
        public int position { get; set; }
    }


    public class tags : List<tag> { }
    public class tag
    {
        public string id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }

    }


}