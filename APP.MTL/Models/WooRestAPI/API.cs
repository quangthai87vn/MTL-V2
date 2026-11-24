using System.ComponentModel;
using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace MTL.APP.WooRestAPI
{
    public static class API
    {
        static HttpClient clientApp = new HttpClient();
        static string username = "";// Settings.Default.Woo_consumer_key;
        static string password = "";// Settings.Default.Woo_consumer_secret;
        static string WooUrl = "";// Settings.Default.Woo_url;
        static string ListProducts = null;
        static API()
        {
            var configuration = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json")
                            .Build();
            username = configuration.GetValue<string>("Woo_consumer_key");
            password = configuration.GetValue<string>("Woo_consumer_secret");
            WooUrl = configuration.GetValue<string>("Woo_url");
        }
        public static DataTable ToDataTable<T>(this IList<T> data)
        {
            PropertyDescriptorCollection props =
            TypeDescriptor.GetProperties(typeof(T));
            DataTable table = new DataTable();
            for (int i = 0; i < props.Count; i++)
            {
                PropertyDescriptor prop = props[i];
                table.Columns.Add(prop.Name, prop.PropertyType);
            }
            object[] values = new object[props.Count];
            foreach (T item in data)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = props[i].GetValue(item);
                }
                table.Rows.Add(values);
            }
            return table;
        }

        public static async Task<List<Product>> GetListProducts(string page, string searchSP, string stock_status)
        {
            DataTable dt = new DataTable();
            string urlAPI = "/wp-json/wc/v3/products?per_page=100";//&on_sale=true
            urlAPI = urlAPI + string.Format("&page={0}&search={1}", page, searchSP);

            if (stock_status != "")
                urlAPI = urlAPI + string.Format("&stock_status={0}", stock_status);
            HttpClientHandler handler = new HttpClientHandler();
            handler.Credentials = new NetworkCredential("test", "testing");
            HttpClient client = new HttpClient(handler);
            client.BaseAddress = new Uri(WooUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            // client per_page
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            HttpResponseMessage response = client.GetAsync(urlAPI).Result;
            var ListProductsString = response.Content.ReadAsStringAsync().Result;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            List<Product> ProductList = JsonConvert.DeserializeObject<Products>(ListProductsString, settings);
            //dt = UserList.ToDataTable<Product>();
            //dt.TableName = "WooProducts";
            return ProductList;
        }


        public static async Task<Product> RetrieveProduct(string id)
        {
            DataTable dt = new DataTable();
            string urlAPI = string.Format("/wp-json/wc/v3/products/{0}", id);

            HttpClientHandler handler = new HttpClientHandler();
            handler.Credentials = new NetworkCredential("test", "testing");
            HttpClient client = new HttpClient(handler);
            client.BaseAddress = new Uri(WooUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            // client per_page
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            HttpResponseMessage response = client.GetAsync(urlAPI).Result;
            var ProductsString = response.Content.ReadAsStringAsync().Result;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            Product produ = JsonConvert.DeserializeObject<Product>(ProductsString, settings);
            return produ;
        }




        public static async Task<Product> UpdateProduct(Product pd)
        {
            string urlAPI = string.Format("/wp-json/wc/v3/products/{0}", pd.id);
            HttpClient client = new HttpClient();

            var company = JsonConvert.SerializeObject(pd, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            var jsonContent = new StringContent(company, Encoding.UTF8, "application/json");

            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            client.BaseAddress = new Uri(WooUrl);
            HttpResponseMessage response = client.PutAsync(urlAPI, jsonContent).Result;
            response.EnsureSuccessStatusCode();
            // var jsonResponse = await response.Content.ReadAsStringAsync();
            var ProductsString = response.Content.ReadAsStringAsync().Result;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            Product produ = JsonConvert.DeserializeObject<Product>(ProductsString, settings);
            return produ;
        }

        public static async Task<Product> CreateProduct(Product pd)
        {
            string urlAPI = string.Format("/wp-json/wc/v3/products");
            HttpClient client = new HttpClient();
            var company = JsonConvert.SerializeObject(pd, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            var jsonContent = new StringContent(company, Encoding.UTF8, "application/json");
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            client.BaseAddress = new Uri(WooUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = client.PostAsync(urlAPI, jsonContent).Result;
            response.EnsureSuccessStatusCode();

            var ProductsString = response.Content.ReadAsStringAsync().Result;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            Product produ = JsonConvert.DeserializeObject<Product>(ProductsString, settings);
            return produ;
        }

        public static async Task<DataTable> GetListProductsCategorys(string page, string searchSP)
        {
            DataTable dt = new DataTable();
            string urlAPI = "/wp-json/wc/v3/products/categories?per_page=100";//&on_sale=true
            urlAPI = urlAPI + string.Format("&page={0}&search={1}", page, searchSP);
            HttpClientHandler handler = new HttpClientHandler();
            handler.Credentials = new NetworkCredential("test", "testing");
            HttpClient client = new HttpClient(handler);
            client.BaseAddress = new Uri(WooUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            // client per_page
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            HttpResponseMessage response = client.GetAsync(urlAPI).Result;
            var ListProductsString = response.Content.ReadAsStringAsync().Result;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            List<Product> UserList = JsonConvert.DeserializeObject<Products>(ListProductsString, settings);
            dt = UserList.ToDataTable<Product>();
            dt.TableName = "ProductsCategorys";
            return dt;
        }

        public static async Task<DataTable> GetListOrders(string page, string search)
        {
            DataTable dt = new DataTable();
            string urlAPI = " /wp-json/wc/v3/orders?per_page=100";//&on_sale=true
            urlAPI = urlAPI + string.Format("&page={0}&search={1}", page, search);

            HttpClientHandler handler = new HttpClientHandler();
            handler.Credentials = new NetworkCredential("test", "testing");
            HttpClient client = new HttpClient(handler);
            client.BaseAddress = new Uri(WooUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            // client per_page
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            HttpResponseMessage response = client.GetAsync(urlAPI).Result;
            var ListProductsString = response.Content.ReadAsStringAsync().Result;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            List<Order> OrderList = JsonConvert.DeserializeObject<Orders>(ListProductsString, settings);
            dt = OrderList.ToDataTable<Order>();
            dt.TableName = "WooOrders";
            return dt;
        }

        public static async Task<DataTable> GetLisProductAttributes(string page, string search)
        {
            DataTable dt = new DataTable();
            string urlAPI = "/wp-json/wc/v3/products/attributes?per_page=100";//&on_sale=true
            urlAPI = urlAPI + string.Format("&page={0}&search={1}", page, search);
            HttpClientHandler handler = new HttpClientHandler();
            handler.Credentials = new NetworkCredential("test", "testing");
            HttpClient client = new HttpClient(handler);
            client.BaseAddress = new Uri(WooUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            // client per_page
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            HttpResponseMessage response = client.GetAsync(urlAPI).Result;
            var ListProductsString = response.Content.ReadAsStringAsync().Result;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            List<Attribute> OrderList = JsonConvert.DeserializeObject<Attributes>(ListProductsString, settings);
            dt = OrderList.ToDataTable<Attribute>();
            dt.TableName = "ProductAttributes";
            return dt;
        }

        public static async Task<DataTable> GetLisProductVariations(string page, string attribute_id)
        {
            DataTable dt = new DataTable();
            string urlAPI = string.Format("/wp-json/wc/v3/products/attributes/{0}/terms?per_page=100", attribute_id);
            // urlAPI = urlAPI + string.Format("&page={0}&search={1}", page, search);
            urlAPI = urlAPI + string.Format("&page={0}", page);
            HttpClientHandler handler = new HttpClientHandler();
            handler.Credentials = new NetworkCredential("test", "testing");
            HttpClient client = new HttpClient(handler);
            client.BaseAddress = new Uri(WooUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            // client per_page
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            HttpResponseMessage response = client.GetAsync(urlAPI).Result;
            var ListProductsString = response.Content.ReadAsStringAsync().Result;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            List<Variation> OrderList = JsonConvert.DeserializeObject<Variations>(ListProductsString, settings);
            dt = OrderList.ToDataTable<Variation>();
            dt.TableName = "ProductAttributes";
            return dt;
        }

        public static async Task<Variation> CreateVariation(string attribute_id, Variation pd)
        {
            // Create an attribute term
            string urlAPI = string.Format(string.Format("/wp-json/wc/v3/products/attributes/{0}/terms", attribute_id));
            HttpClient client = new HttpClient();
            var company = JsonConvert.SerializeObject(pd, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            var jsonContent = new StringContent(company, Encoding.UTF8, "application/json");
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            client.BaseAddress = new Uri(WooUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = client.PostAsync(urlAPI, jsonContent).Result;
            response.EnsureSuccessStatusCode();

            var ProductsString = response.Content.ReadAsStringAsync().Result;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            Variation produ = JsonConvert.DeserializeObject<Variation>(ProductsString, settings);
            return produ;
        }


    }
}
