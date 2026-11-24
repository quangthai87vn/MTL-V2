namespace MTL.APP.WooRestAPI
{

    public class Orders : List<Order> { }
    public class Order
    {
        public string id { get; set; }
        public string total { get; set; }
        public string date_created { get; set; }
        //public string description { get; set; }
        //public string short_description { get; set; }
        //public string stock_status { get; set; }

        //public string price { get; set; }
        //public string regular_price { get; set; }
        //public string sale_price { get; set; }
        //public string sku { get; set; }

        //public string type { get; set; }  
        //public List<Order> categories { get; set; }

        //public string slug { get; set; } // link trang web


    }



}
