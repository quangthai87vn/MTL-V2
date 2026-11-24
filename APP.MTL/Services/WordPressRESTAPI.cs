using Blazored.LocalStorage;  
using Microsoft.AspNetCore.Routing; using Microsoft.Extensions.Options; using MTL.APP.WooRestAPI; using Newtonsoft.Json; using System;
using System.Net; using System.Net.Http; using System.Net.Http.Headers; using System.Text; using WordPressPCL; using WordPressPCL.Models; using WordPressPCL.Utility;  namespace MTL.APP.Services {     public class WordPressRESTAPI
	{         public HttpClient _httpClient { get; }         //public AppSettings _appSettings { get; }         public ILocalStorageService _localStorageService { get; }         public string username = string.Empty;// Settings.Default.Woo_consumer_key;         public string password = string.Empty;// Settings.Default.Woo_consumer_secret;         public string WooUrl = string.Empty;// Settings.Default.Woo_url;          public WordPressRESTAPI(HttpClient httpClient            //, IOptions<AppSettings> appSettings            //, ILocalStorageService localStorageService             )         {             //_appSettings = appSettings.Value;             //_localStorageService = localStorageService;             //httpClient.BaseAddress = new Uri(_appSettings.MTLBaseAddress);             httpClient.DefaultRequestHeaders.Add("User-Agent", "BlazorServer");             _httpClient = httpClient;             //var configuration = new ConfigurationBuilder()             //               .AddJsonFile("appsettings.json")             //               .Build();             //username = configuration.GetValue<string>("Woo_consumer_key");             //password = configuration.GetValue<string>("Woo_consumer_secret");             //WooUrl = configuration.GetValue<string>("Woo_url");         }
		public async Task<string> GetAsync(string url,string route, string username, string password)
        {				
			HttpClient clientapp = new HttpClient();
			clientapp.BaseAddress = new Uri(url);
			clientapp.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
			string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
			clientapp.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");				
			HttpResponseMessage response = clientapp.GetAsync(route).Result;
			var contentstring = response.Content.ReadAsStringAsync().Result;
            return contentstring;
		}
		
		public async Task<List<MTL.APP.WordPressRestAPI.Post>> GetPostsAsync(string url, string username, string password)         {            
			string route = "/wp-json/wp/v2/posts"+ "?per_page=100&status=publish, future, draft, pending, private";
            string contentstring = await this.GetAsync(url, route, username, password);			
			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				MissingMemberHandling = MissingMemberHandling.Ignore
			};             List<MTL.APP.WordPressRestAPI.Post> data = JsonConvert.DeserializeObject<MTL.APP.WordPressRestAPI.Posts>(contentstring, settings);                         return data;         }

		public async Task<MTL.APP.WordPressRestAPI.Post> CreatePostsAsync(string url, string username, string password, MTL.APP.WordPressRestAPI.Post post)
		{
			string route = "/wp-json/wp/v2/posts";			
			HttpClient clientapp = new HttpClient();
			var postdata = JsonConvert.SerializeObject(post, new JsonSerializerSettings()
			{
				NullValueHandling = NullValueHandling.Ignore
			});
			var jsonContent = new StringContent(postdata, Encoding.UTF8, "application/json");
			clientapp.BaseAddress = new Uri(url);
			clientapp.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
			string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
			clientapp.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");			
			HttpResponseMessage response = clientapp.PostAsync(route, jsonContent).Result;             //response.EnsureSuccessStatusCode();                       var contentstring = response.Content.ReadAsStringAsync().Result;					
			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				MissingMemberHandling = MissingMemberHandling.Ignore
			};
			MTL.APP.WordPressRestAPI.Post data = JsonConvert.DeserializeObject<MTL.APP.WordPressRestAPI.Post>(contentstring, settings);
			return data;
		}
		
		public async Task<MTL.APP.WordPressRestAPI.Post> UpdatePostsAsync(string url, string username, string password, MTL.APP.WordPressRestAPI.Post post)
		{
			string route = "/wp-json/wp/v2/posts/"+post.id;			
			HttpClient clientapp = new HttpClient();
			var postdata = JsonConvert.SerializeObject(post, new JsonSerializerSettings()
			{
				NullValueHandling = NullValueHandling.Ignore
			});
			var jsonContent = new StringContent(postdata, Encoding.UTF8, "application/json");
			clientapp.BaseAddress = new Uri(url);
			clientapp.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
			string userAndPasswordToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
			clientapp.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {userAndPasswordToken}");
			
			HttpResponseMessage response = clientapp.PostAsync(route, jsonContent).Result;             //response.EnsureSuccessStatusCode();                       var contentstring = response.Content.ReadAsStringAsync().Result;  					
			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				MissingMemberHandling = MissingMemberHandling.Ignore
			};
			MTL.APP.WordPressRestAPI.Post data = JsonConvert.DeserializeObject<MTL.APP.WordPressRestAPI.Post>(contentstring, settings);
			return data;
		}
		public async Task<MTL.APP.WordPressRestAPI.Post> RetrievePostsAsync(string url, string username, string password,int postid)
		{
			string route = "/wp-json/wp/v2/posts/"+ postid.ToString();
			string contentstring = await this.GetAsync(url, route, username, password);
			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				MissingMemberHandling = MissingMemberHandling.Ignore
			};
			MTL.APP.WordPressRestAPI.Post post = JsonConvert.DeserializeObject<MTL.APP.WordPressRestAPI.Post>(contentstring, settings);
			return post;
		}

	} } 