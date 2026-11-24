using Blazored.LocalStorage;
using Microsoft.Extensions.Options;
using MTL.APP.WooRestAPI;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace MTL.APP.Services
{
    public class APIWooService
    {
        public HttpClient _httpClient { get; }
        //public AppSettings _appSettings { get; }
        public ILocalStorageService _localStorageService { get; }
        public string username = "";// Settings.Default.Woo_consumer_key;
        public string password = "";// Settings.Default.Woo_consumer_secret;
        public string WooUrl = "";// Settings.Default.Woo_url;

        public APIWooService(HttpClient httpClient
           //, IOptions<AppSettings> appSettings
           //, ILocalStorageService localStorageService
            )
        {
            //_appSettings = appSettings.Value;
            //_localStorageService = localStorageService;
            //httpClient.BaseAddress = new Uri(_appSettings.MTLBaseAddress);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "BlazorServer");
            _httpClient = httpClient;
            //var configuration = new ConfigurationBuilder()
            //               .AddJsonFile("appsettings.json")
            //               .Build();
            //username = configuration.GetValue<string>("Woo_consumer_key");
            //password = configuration.GetValue<string>("Woo_consumer_secret");
            //WooUrl = configuration.GetValue<string>("Woo_url");
        }
        public async Task<List<Product>> GetListProductsAsync(string page, string searchSP, string stock_status, string Wookey, string Woosecret, string WooUrl)
        {
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
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(Wookey + ":" + Woosecret));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            HttpResponseMessage response = client.GetAsync(urlAPI).Result;
            var ListProductsString = response.Content.ReadAsStringAsync().Result;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            List<Product> ProductList = JsonConvert.DeserializeObject<Products>(ListProductsString, settings);
            
            return ProductList;
        }
        
        public async Task<List<Product>> GetListProductsByPageAsync(int per_page, int page, string searchSP, string stock_status,string status, string orderby, string Wookey, string Woosecret, string WooUrl)
        {
            string _KetQua = "";          
            string urlAPI = string.Format("/wp-json/wc/v3/products?per_page={0}&page={1}&search={2}&status={3}", per_page, page, searchSP, status);
			if (stock_status != "")
				urlAPI = urlAPI + string.Format("&stock_status={0}", stock_status);
			var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
			List<Product> ProductList=new List<Product>();
			_KetQua = (await ExcuteWooAPIAsync(urlAPI, Wookey, Woosecret, WooUrl));
			ProductList = JsonConvert.DeserializeObject<Products>(_KetQua, settings);
			return ProductList;		
		}

        public async Task<ProductCount> GetProductCountAsync(string Wookey, string Woosecret, string WooUrl)
        {
            string _KetQua = "";
            string urlAPI_Count = "/wc-api/v3/products/count";
            _KetQua = (await ExcuteWooAPIAsync(urlAPI_Count, Wookey, Woosecret, WooUrl));
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            ProductCount ProductCount = JsonConvert.DeserializeObject<ProductCount>(_KetQua, settings);
            return ProductCount;
        }
        public async Task<string> ExcuteWooAPIAsync(string urlAPI,string Wookey, string Woosecret, string WooUrl)
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.Credentials = new NetworkCredential("test", "testing");
            HttpClient client = new HttpClient(handler);
            client.BaseAddress = new Uri(WooUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            // client per_page
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(Wookey + ":" + Woosecret));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            HttpResponseMessage response = client.GetAsync(urlAPI).Result;
            string _KetQua = response.Content.ReadAsStringAsync().Result;         
            return _KetQua;
        }

        public async Task<Product> RetrieveProductAsync(string id, string Wookey, string Woosecret, string WooUrl)
        {
			string requestUri = string.Format("/wp-json/wc/v3/products/{0}", id);		
            HttpClientHandler handler = new HttpClientHandler();
            handler.Credentials = new NetworkCredential("test", "testing");
            HttpClient client = new HttpClient(handler);
            client.BaseAddress = new Uri(WooUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            // client per_page
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(Wookey + ":" + Woosecret));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            HttpResponseMessage response = client.GetAsync(requestUri).Result;
            var responseBody = response.Content.ReadAsStringAsync().Result;
            var returnedList = JsonConvert.DeserializeObject<Product>(responseBody);
            return await Task.FromResult(returnedList);
        }

		public async Task<Product> UpdateProductAsync(Product pd, string Wookey, string Woosecret, string WooUrl)
        {
            string urlAPI = string.Format("/wp-json/wc/v3/products/{0}", pd.id);
            HttpClient client = new HttpClient();
            var company = JsonConvert.SerializeObject(pd, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            var jsonContent = new StringContent(company, Encoding.UTF8, "application/json");
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(Wookey + ":" + Woosecret));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            client.BaseAddress = new Uri(WooUrl);
            HttpResponseMessage response = client.PutAsync(urlAPI, jsonContent).Result;
            response.EnsureSuccessStatusCode();
            //var jsonResponse = await response.Content.ReadAsStringAsync();
            var ProductsString = response.Content.ReadAsStringAsync().Result;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            Product produ = JsonConvert.DeserializeObject<Product>(ProductsString, settings);
            return produ;
        }

        public async Task<Product> CreateProductAsync(Product pd, string Wookey, string Woosecret, string WooUrl)
        {
            string urlAPI = string.Format("/wp-json/wc/v3/products");
            HttpClient client = new HttpClient();
            var company = JsonConvert.SerializeObject(pd, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            var jsonContent = new StringContent(company, Encoding.UTF8, "application/json");
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(Wookey + ":" + Woosecret));
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


        public async Task<List<Product>> GetListProductsCategorys(string Wookey, string Woosecret, string WooUrl, string page, string searchSP)
        {
            string urlAPI = "/wp-json/wc/v3/products/categories?per_page=100";//&on_sale=true
            urlAPI = urlAPI + string.Format("&page={0}&search={1}", page, searchSP);
            HttpClientHandler handler = new HttpClientHandler();
            handler.Credentials = new NetworkCredential("test", "testing");
            HttpClient client = new HttpClient(handler);
            client.BaseAddress = new Uri(WooUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            // client per_page
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(Wookey + ":" + Woosecret));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            HttpResponseMessage response = client.GetAsync(urlAPI).Result;
            var ListProductsString = response.Content.ReadAsStringAsync().Result;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            List<Product> _List = JsonConvert.DeserializeObject<Products>(ListProductsString, settings);
            return _List;
        }
        public async Task<bool> DeleteProductAsync(string wooid_product, string Wookey, string Woosecret, string WooUrl)
        {
            string urlAPI = string.Format("/wp-json/wc/v3/products/{0}", wooid_product);
            HttpClient client = new HttpClient();            
            string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(Wookey + ":" + Woosecret));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
            client.BaseAddress = new Uri(WooUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")); 
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, urlAPI); 
            var response = await client.SendAsync(requestMessage);
            var responseStatusCode = response.StatusCode;
            return await Task.FromResult(true);
        }




    }
}
