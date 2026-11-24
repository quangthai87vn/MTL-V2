namespace MTL.APP.WooRestAPI
{
    public class Products : List<Product> { }
    public class Product
    {
        public string id { get; set; }
        public string name { get; set; }
        public string permalink { get; set; }
        public string description { get; set; }
        public string short_description { get; set; }
        public string stock_status { get; set; }
        public string price { get; set; }
        public string regular_price { get; set; }
        public string sale_price { get; set; }
        public string sku { get; set; }

        //public DateTime date_modified { get; set; }
        //public DateTime date_modified_gmt { get; set; }
        //public DateTime date_created { get; set; }
        //public DateTime date_created_gmt { get; set; }
        public string type { get; set; }
        public List<Category> categories { get; set; }
        public string slug { get; set; } // link trang web
        public List<image> images { get; set; }
        public List<tag> tags { get; set; }
        public List<Attribute> attributes { get; set; }
    }

    public class Product_NonDate
    {
        public string id { get; set; }
        public string name { get; set; }
        public string permalink { get; set; }
        public string description { get; set; }
        public string short_description { get; set; }
        public string stock_status { get; set; }

        public string price { get; set; }
        public string regular_price { get; set; }
        public string sale_price { get; set; }
        public string sku { get; set; }
        public DateTime date_modified { get; set; }
        public DateTime date_modified_gmt { get; set; }
        public DateTime date_created { get; set; }
        public DateTime date_created_gmt { get; set; }
        public string type { get; set; }
        public List<Category> categories { get; set; }
        public string slug { get; set; } // link trang web
        public List<image> images { get; set; }
        public List<tag> tags { get; set; }
        public List<Attribute> attributes { get; set; }
    }


    public class Attributes : List<Attribute> { }
    public class Attribute
    {
        public string id { get; set; }
        public string name { get; set; }
        public string position { get; set; }
        public List<string> options { get; set; }
        public string option { get; set; }
        public string visible { get; set; }

    }

    public class Variations : List<Variation> { }
    public class Variation
    {
        public string id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }

    }

    public class ProductCount
    {
        public int count { get; set; }

    }
}
