using MTL.APP.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MTL.APP.Services
{
    public interface IZaloOaService
    {
        Task<List<ZaloOaConversation>> GetConversationsAsync(int count = 20);
        Task<List<ZaloOaMessage>> GetMessagesAsync(string userId, int offset = 0, int limit = 10);
        Task<(bool Success, string? MessageId, string? Error)> SendTextMessageAsync(string userId, string messageText);
        Task<(bool Success, string? MessageId, string? Error)> SendImageMessageAsync(string userId, string imageToken);
        Task<(bool Success, string? Token, string? Error)> UploadMediaAsync(string filePath, string type = "image");
        Task<(bool Success, string? Token, string? Error)> UploadMediaFromUrlAsync(string url, string type = "image");
        string? GetAccessToken();
        string? GetDebugAccessToken();
        bool IsConfigured { get; }
        Task<ZaloOaFollowerItem?> GetOaInfoAsync();
        Task<N8NTokenResult> ForceFetchTokenFromN8NAsync();
    }

    public record N8NTokenResult(bool Success, string? AccessToken, string? RefreshToken, int? ExpiresIn, string? Error, string? RawResponse, bool ExpandRawResponse = false);

    public class ZaloOaService : IZaloOaService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ZaloOaService> _logger;

        private const string ApiBaseUrl = "https://openapi.zalo.me/v2.0";
        private const string OAuthBaseUrl = "https://oauth.zaloapp.com/v4/oa";

        private readonly string _tokenFilePath;
        private string? _cachedAccessToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        private string N8NBaseUrl => _configuration["ZaloSettings:N8NBaseUrl"]?.TrimEnd('/') ?? "";
        private bool UseN8NToken => !string.IsNullOrEmpty(N8NBaseUrl);

        public ZaloOaService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ZaloOaService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;

            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MTL.APP");
            Directory.CreateDirectory(appDataPath);
            _tokenFilePath = Path.Combine(appDataPath, "zalo_oa_tokens.json");
        }

        public bool IsConfigured =>
            !string.IsNullOrEmpty(_configuration["ZaloSettings:AppId"]) &&
            !string.IsNullOrEmpty(_configuration["ZaloSettings:AppSecret"]);

        private string AppId => _configuration["ZaloSettings:AppId"] ?? "";
        private string AppSecret => _configuration["ZaloSettings:AppSecret"] ?? "";

        private class StoredTokens
        {
            public string? AccessToken { get; set; }
            public DateTime ExpiresAt { get; set; }
            public string? RefreshToken { get; set; }
            public DateTime RefreshExpiresAt { get; set; }
        }

        private StoredTokens LoadTokens()
        {
            try
            {
                if (File.Exists(_tokenFilePath))
                {
                    var json = File.ReadAllText(_tokenFilePath);
                    return JsonSerializer.Deserialize<StoredTokens>(json) ?? new StoredTokens();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Zalo tokens from file");
            }
            return new StoredTokens();
        }

        private void SaveTokens(StoredTokens tokens)
        {
            try
            {
                var json = JsonSerializer.Serialize(tokens, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_tokenFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save Zalo tokens to file");
            }
        }

        public async Task<string?> EnsureValidAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiry)
                return _cachedAccessToken;

            if (!IsConfigured)
            {
                _logger.LogWarning("Zalo OA is not configured (missing AppId/AppSecret)");
                return null;
            }

            // 1. Thu tu uu tien: goi N8N de lay token tu Google Sheet
            if (UseN8NToken)
            {
                var n8nToken = await GetTokenFromN8NAsync();
                if (!string.IsNullOrEmpty(n8nToken))
                {
                    _cachedAccessToken = n8nToken;
                    _tokenExpiry = DateTime.UtcNow.AddMinutes(55);
                    _logger.LogInformation("Got Zalo access token from N8N webhook");
                    return _cachedAccessToken;
                }
            }

            // 2. Fallback: kiem tra token da luu local
            var stored = LoadTokens();
            if (!string.IsNullOrEmpty(stored.AccessToken) && DateTime.UtcNow < stored.ExpiresAt)
            {
                _cachedAccessToken = stored.AccessToken;
                _tokenExpiry = stored.ExpiresAt;
                _logger.LogInformation("Using cached access token (expires: {Expiry})", stored.ExpiresAt);
                return _cachedAccessToken;
            }

            // 3. Fallback: refresh token
            if (!string.IsNullOrEmpty(stored.RefreshToken) && DateTime.UtcNow < stored.RefreshExpiresAt)
            {
                _logger.LogInformation("Refreshing Zalo OA access token (refresh expires: {Expiry})", stored.RefreshExpiresAt);
                var refreshed = await RefreshTokenAsync(stored.RefreshToken);
                if (refreshed != null) return refreshed;
            }

            // 4. Fallback cuoi: exchange authorization code
            var authCode = _configuration["ZaloSettings:AuthorizationCode"];
            if (!string.IsNullOrEmpty(authCode))
            {
                _logger.LogInformation("Exchanging authorization code for access token (code length: {Len})", authCode.Length);
                var result = await GetAccessTokenFromCodeAsync(authCode);
                if (result != null) return result;
            }

            _logger.LogWarning("No valid Zalo OA access token available");
            return null;
        }

        private async Task<string?> GetTokenFromN8NAsync()
        {
            try
            {
                var n8nUrl = $"{N8NBaseUrl}/webhook/zalo-token";
            _logger.LogInformation("Fetching Zalo token from N8N: {Url}", n8nUrl);
            _logger.LogDebug("N8N request headers will be sent");

            var client = _httpClientFactory.CreateClient("N8N");
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync(n8nUrl);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("N8N raw response ({Status}): {Body}", response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("N8N webhook returned {Status}: {Body}", response.StatusCode, responseBody);
                return null;
            }

            var json = JsonDocument.Parse(responseBody);
            var root = json.RootElement;

            if (root.TryGetProperty("access_token", out var tokenProp) &&
                !string.IsNullOrWhiteSpace(tokenProp.GetString()))
            {
                var tokenValue = tokenProp.GetString()!;
                _logger.LogInformation("Got token from N8N: {Length} chars, prefix: {Prefix}",
                    tokenValue.Length, tokenValue[..Math.Min(8, tokenValue.Length)]);
                return tokenValue;
            }

                _logger.LogWarning("N8N response does not contain valid access_token");
                return null;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("N8N token fetch timed out");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get token from N8N");
                return null;
            }
        }

        public string? GetAccessToken()
        {
            if (string.IsNullOrEmpty(_cachedAccessToken) || DateTime.UtcNow >= _tokenExpiry)
            {
                var task = EnsureValidAccessTokenAsync();
                if (!task.IsCompleted)
                    task.Wait(TimeSpan.FromSeconds(5));
            }
            return _cachedAccessToken;
        }

        public string? GetDebugAccessToken() => _cachedAccessToken;

        /// <summary>
        /// Force fetch token from N8N webhook and return detailed result for UI display.
        /// </summary>
        public async Task<N8NTokenResult> ForceFetchTokenFromN8NAsync()
        {
            if (!UseN8NToken)
            {
                return new N8NTokenResult(false, null, null, null, "N8NBaseUrl chua cau hinh trong appsettings.json", null);
            }

            try
            {
                var n8nUrl = $"{N8NBaseUrl}/webhook/zalo-token";
                _logger.LogInformation("Force fetching Zalo token from N8N: {Url}", n8nUrl);

                var client = _httpClientFactory.CreateClient("N8N");
                client.Timeout = TimeSpan.FromSeconds(15);

                var response = await client.GetAsync(n8nUrl);
                var rawBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("N8N webhook returned {Status}: {Body}", response.StatusCode, rawBody);
                    return new N8NTokenResult(false, null, null, null,
                        $"N8N webhook error {response.StatusCode}", rawBody, false);
                }

                var json = JsonDocument.Parse(rawBody);
                var root = json.RootElement;

                string? accessToken = null;
                string? refreshToken = null;
                int? expiresIn = null;
                string? error = null;

                if (root.TryGetProperty("access_token", out var tokenProp) &&
                    !string.IsNullOrWhiteSpace(tokenProp.GetString()))
                {
                    accessToken = tokenProp.GetString();
                }

                if (root.TryGetProperty("refresh_token", out var rtProp) &&
                    !string.IsNullOrWhiteSpace(rtProp.GetString()))
                {
                    refreshToken = rtProp.GetString();
                }

                if (root.TryGetProperty("expires_in", out var eiProp))
                {
                    expiresIn = eiProp.GetInt32();
                }

                if (root.TryGetProperty("error", out var errProp))
                {
                    error = errProp.GetString();
                }

                if (string.IsNullOrEmpty(accessToken))
                {
                    return new N8NTokenResult(false, null, null, null,
                        "N8N response khong co access_token", rawBody, false);
                }

                // Cache the token
                _cachedAccessToken = accessToken;
                _tokenExpiry = expiresIn.HasValue
                    ? DateTime.UtcNow.AddSeconds(expiresIn.Value - 60)
                    : DateTime.UtcNow.AddMinutes(55);

                _logger.LogInformation("Force-fetched token from N8N: prefix={Prefix}, expiresIn={ExpiresIn}s",
                    accessToken[..Math.Min(8, accessToken.Length)], expiresIn);

                return new N8NTokenResult(true, accessToken, refreshToken, expiresIn, null, rawBody, false);
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("N8N token fetch timed out");
                return new N8NTokenResult(false, null, null, null, "N8N webhook timed out", null, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to force-fetch token from N8N");
                return new N8NTokenResult(false, null, null, null, ex.Message, null, false);
            }
        }

        private async Task<string?> GetAccessTokenFromCodeAsync(string authorizationCode)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["app_id"] = AppId,
                    ["grant_type"] = "authorization_code",
                    ["code"] = authorizationCode,
                    ["app_secret"] = AppSecret
                });

                var response = await client.PostAsync($"{OAuthBaseUrl}/access_token", content);
                var json = await response.Content.ReadFromJsonAsync<ZaloOaTokenResponse>();

                if (json == null || json.Error != 0 || string.IsNullOrEmpty(json.AccessToken))
                {
                    _logger.LogError("Failed to get access token: {Message}", json?.Message);
                    return null;
                }

                var tokens = new StoredTokens
                {
                    AccessToken = json.AccessToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(json.ExpiresIn - 300),
                    RefreshToken = json.RefreshToken,
                    RefreshExpiresAt = json.RefreshTokenExpiresIn > 0
                        ? DateTime.UtcNow.AddSeconds(json.RefreshTokenExpiresIn - 300)
                        : DateTime.UtcNow.AddDays(89)
                };
                SaveTokens(tokens);

                _cachedAccessToken = json.AccessToken;
                _tokenExpiry = tokens.ExpiresAt;
                _logger.LogInformation("Successfully obtained new Zalo OA access token");
                return json.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting access token from authorization code");
                return null;
            }
        }

        private async Task<string?> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["app_id"] = AppId,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["app_secret"] = AppSecret
                });

                var response = await client.PostAsync($"{OAuthBaseUrl}/refresh_token", content);
                var json = await response.Content.ReadFromJsonAsync<ZaloOaTokenRefreshResponse>();

                if (json == null || json.Error != 0 || string.IsNullOrEmpty(json.AccessToken))
                {
                    _logger.LogError("Failed to refresh token: {Message}", json?.Message);
                    return null;
                }

                var stored = LoadTokens();
                stored.AccessToken = json.AccessToken;
                stored.ExpiresAt = DateTime.UtcNow.AddSeconds(json.ExpiresIn - 300);
                SaveTokens(stored);

                _cachedAccessToken = json.AccessToken;
                _tokenExpiry = stored.ExpiresAt;
                _logger.LogInformation("Successfully refreshed Zalo OA access token");
                return json.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing Zalo OA token");
                return null;
            }
        }

        private void ClearTokenCache()
        {
            _cachedAccessToken = null;
            _tokenExpiry = DateTime.MinValue;
            _logger.LogInformation("Token cache cleared");
        }

        /// <summary>
        /// Lấy danh sách cuộc trò chuyện từ Zalo OA API
        /// Endpoint: GET /v2.0/oa/listrecentchat
        /// </summary>
        public async Task<List<ZaloOaConversation>> GetConversationsAsync(int count = 10)
        {
            var token = await EnsureValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No access token for GetConversationsAsync");
                throw new InvalidOperationException("Khong lay duoc access token tu N8N hoac Zalo. Kiem tra N8N webhook va ZaloSettings.");
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            var payload = new { offset = 0, count };
            var encodedData = Uri.EscapeDataString(JsonSerializer.Serialize(payload));
            var url = $"{ApiBaseUrl}/oa/listrecentchat?access_token={token}&data={encodedData}";

            _logger.LogInformation("Calling Zalo API: {Url}", url);

            var response = await client.GetAsync(url);

            var rawBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GetConversations failed: HTTP {Status} - {Body}", response.StatusCode, rawBody);
                throw new InvalidOperationException($"Zalo API HTTP {response.StatusCode}: {rawBody}");
            }

            var json = JsonDocument.Parse(rawBody);
            var root = json.RootElement;
            _logger.LogInformation("Zalo API raw response (first 500 chars): {Response}", rawBody.Length > 500 ? rawBody[..500] : rawBody);

            if (root.TryGetProperty("error", out var err) && err.GetInt32() != 0)
            {
                var msg = root.TryGetProperty("message", out var em) ? em.GetString() : "unknown";
                var errorCode = err.GetInt32();

                if (errorCode == -216 || errorCode == 452)
                {
                    _logger.LogWarning("Zalo token expired ({Code}), clearing cache", errorCode);
                    ClearTokenCache();
                }

                _logger.LogError("GetConversations Zalo error {Code}: {Msg}", errorCode, msg);

                var userHint = errorCode switch
                {
                    404 => "OA khong co du lieu hoi thoai. Dam bao da co nguoi dung nhan tin den Zalo OA.",
                    -216 => "Token het han - N8N se lay token moi tu Google Sheet.",
                    -210 => "So luong toi da (max 10). Thu giam so luong.",
                    -200 => "API khong ho tro phuong thuc nay. Thu doi thanh GET.",
                    -5 => "Sai AppId hoac AppSecret - kiem tra ZaloSettings trong appsettings.json.",
                    -201 => "Khong co quyen truy cap API nay - kiem tra app permissions trong Zalo Developer Portal.",
                    452 => "Token da het han hoac bi thu hoi. Can lay token moi tu N8N / Google Sheet.",
                    _ => $"Loi {errorCode} tu Zalo API."
                };
                throw new InvalidOperationException($"Zalo error {errorCode}: {msg}. {userHint}");
            }

            var conversations = new List<ZaloOaConversation>();

            // Zalo OA listrecentchat tra ve data la mang truc tiep
            // {"data":[{"src":1,"time":...,"from_id":"...","from_display_name":"...","message":"..."},...],"error":0}
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in data.EnumerateArray())
                {
                    var src = c.TryGetProperty("src", out var s) && s.GetInt32() == 1;
                    var userId = src
                        ? (c.TryGetProperty("from_id", out var fid) ? fid.GetString() : null)
                        : (c.TryGetProperty("to_id", out var tid) ? tid.GetString() : null);
                    if (string.IsNullOrEmpty(userId)) continue;

                    var userName = src
                        ? (c.TryGetProperty("from_display_name", out var fdn) ? fdn.GetString() : "Khach hang")
                        : (c.TryGetProperty("to_display_name", out var tdn) ? tdn.GetString() : "Khach hang");
                    var avatar = src
                        ? (c.TryGetProperty("from_avatar", out var fav) ? fav.GetString() : null)
                        : (c.TryGetProperty("to_avatar", out var tav) ? tav.GetString() : null);
                    var msgText = c.TryGetProperty("message", out var mt) ? mt.GetString() ?? "" : "";
                    var msgTime = default(DateTime);
                    var unreadCount = 0;
                    var totalMsgCount = 0;

                    if (c.TryGetProperty("time", out var ts))
                    {
                        var tsInt = ts.GetInt64();
                        msgTime = DateTimeOffset.FromUnixTimeMilliseconds(tsInt).LocalDateTime;
                    }

                    if (c.TryGetProperty("unread", out var ur))
                    {
                        unreadCount = ur.GetInt32();
                    }

                    if (c.TryGetProperty("msg_count", out var mc))
                    {
                        totalMsgCount = mc.GetInt32();
                    }

                    conversations.Add(new ZaloOaConversation
                    {
                        UserId = userId,
                        UserName = userName ?? "Khach hang",
                        UserAvatar = avatar,
                        LastMessage = msgText,
                        LastMessageTime = msgTime,
                        UnreadCount = unreadCount,
                        TotalMessages = totalMsgCount
                    });
                }
            }

            _logger.LogInformation("GetConversations: found {Count} conversations", conversations.Count);
            return conversations;
        }

        /// <summary>
        /// Lấy tin nhắn của một user từ Zalo OA API
        /// Endpoint: GET /v2.0/oa/conversation
        /// </summary>
        public async Task<List<ZaloOaMessage>> GetMessagesAsync(string userId, int offset = 0, int limit = 10)
        {
            var token = await EnsureValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No access token for GetMessagesAsync");
                throw new InvalidOperationException("Khong lay duoc access token tu N8N hoac Zalo.");
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            var payload = new { user_id = userId, offset, count = limit };
            var encodedData = Uri.EscapeDataString(JsonSerializer.Serialize(payload));
            var url = $"{ApiBaseUrl}/oa/conversation?access_token={token}&data={encodedData}";

            _logger.LogInformation("Calling Zalo API: {Url}", url);

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("GetMessages failed for {UserId}: {Status} - {Body}", userId, response.StatusCode, errorBody);
                throw new InvalidOperationException($"Zalo API error {response.StatusCode}: {errorBody}");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (json.TryGetProperty("error", out var err) && err.GetInt32() != 0)
            {
                var errMsg = json.TryGetProperty("message", out var em) ? em.GetString() : "unknown";
                var errorCode = err.GetInt32();

                if (errorCode == -216 || errorCode == 452)
                {
                    _logger.LogWarning("Zalo token expired ({Code}), clearing cache", errorCode);
                    ClearTokenCache();
                }

                _logger.LogError("GetMessages Zalo error for {UserId}: {Code} - {Msg}", userId, errorCode, errMsg);
                throw new InvalidOperationException($"Zalo error {errorCode}: {errMsg}");
            }

            var messages = new List<ZaloOaMessage>();

            if (json.TryGetProperty("data", out var data))
            {
                var msgsArr = data.TryGetProperty("messages", out var msgProp) ? msgProp : data;
                foreach (var msgItem in msgsArr.EnumerateArray())
                {
                    var msgTypeInt = msgItem.TryGetProperty("type", out var t) ? t.GetInt32() : 1;
                    var msgType = msgTypeInt switch
                    {
                        1 => "text",
                        2 => "image",
                        3 => "video",
                        4 => "file",
                        5 => "audio",
                        7 => "link",
                        8 => "sticker",
                        9 => "contact",
                        10 => "location",
                        _ => "text"
                    };
                    var msgText = msgItem.TryGetProperty("message", out var txt) ? txt.GetString() ?? "" : "";
                    var msgId = msgItem.TryGetProperty("msg_id", out var mid) ? mid.GetString() : null;
                    var fromMe = msgItem.TryGetProperty("from_me", out var fm) && fm.GetBoolean();
                    var time = default(DateTime);

                    if (msgItem.TryGetProperty("timestamp", out var ts))
                    {
                        var tsInt = ts.GetInt64();
                        time = DateTimeOffset.FromUnixTimeSeconds(tsInt).LocalDateTime;
                    }

                    string? mediaUrl = null;
                    if (msgItem.TryGetProperty("media", out var media) && media.ValueKind != JsonValueKind.Undefined)
                    {
                        mediaUrl = media.TryGetProperty("url", out var url2) ? url2.GetString() : null;
                    }
                    if (string.IsNullOrEmpty(mediaUrl) && msgItem.TryGetProperty("thumb", out var thumb))
                    {
                        mediaUrl = thumb.GetString();
                    }

                    messages.Add(new ZaloOaMessage
                    {
                        UserId = userId,
                        Message = msgText,
                        MessageType = msgType,
                        ZaloMessageId = msgId,
                        IsFromCustomer = !fromMe,
                        IsRead = true,
                        Timestamp = time,
                        MediaUrl = mediaUrl
                    });
                }
            }

            _logger.LogInformation("GetMessages for {UserId}: found {Count} messages", userId, messages.Count);
            return messages;
        }

        /// <summary>
        /// Lấy thông tin OA (Official Account) của mình
        /// </summary>
        public async Task<ZaloOaFollowerItem?> GetOaInfoAsync()
        {
            var token = await EnsureValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
                return null;

            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{ApiBaseUrl}/oa/getprofile?access_token={token}";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("error", out var err) && err.GetInt32() != 0)
                    return null;

                return new ZaloOaFollowerItem
                {
                    UserId = json.TryGetProperty("id", out var id) ? id.GetString() : null,
                    UserName = json.TryGetProperty("name", out var n) ? n.GetString() : null,
                    Avatar = json.TryGetProperty("avatar", out var av) ? av.GetString() : null,
                    ThumbAvatar = json.TryGetProperty("thumb_avatar", out var ta) ? ta.GetString() : null,
                    Phone = json.TryGetProperty("phone", out var ph) ? ph.GetString() : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OA info");
                return null;
            }
        }

        /// <summary>
        /// Gửi tin nhắn text đến user
        /// </summary>
        public async Task<(bool Success, string? MessageId, string? Error)> SendTextMessageAsync(string userId, string messageText)
        {
            var token = await EnsureValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Cannot send message - no access token");
                return (false, null, "No access token");
            }

            try
            {
                var client = _httpClientFactory.CreateClient();

                var body = new
                {
                    recipient = new { user_id = userId },
                    message = new { text = messageText }
                };

                var response = await client.PostAsync(
                    $"{ApiBaseUrl}/oa/message?access_token={token}",
                    new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Send message failed: {Error}", error);
                    return (false, null, error);
                }

                var respJson = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (respJson.TryGetProperty("error", out var err) && err.GetInt32() != 0)
                {
                    var msg = respJson.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
                    return (false, null, msg);
                }

                string? msgId = null;
                if (respJson.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("message_id", out var mid))
                {
                    msgId = mid.GetString();
                }

                _logger.LogInformation("Sent Zalo message to {UserId}: {MessageId}", userId, msgId);
                return (true, msgId, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Zalo message to {UserId}", userId);
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Gửi tin nhắn hình ảnh đến user
        /// </summary>
        public async Task<(bool Success, string? MessageId, string? Error)> SendImageMessageAsync(string userId, string imageToken)
        {
            var token = await EnsureValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
                return (false, null, "No access token");

            try
            {
                var client = _httpClientFactory.CreateClient();

                var body = new
                {
                    recipient = new { user_id = userId },
                    message = new
                    {
                        attachment = new
                        {
                            type = "image",
                            payload = new { token = imageToken }
                        }
                    }
                };

                var response = await client.PostAsync(
                    $"{ApiBaseUrl}/oa/message?access_token={token}",
                    new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return (false, null, error);
                }

                var respJson = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (respJson.TryGetProperty("error", out var err) && err.GetInt32() != 0)
                {
                    var msg = respJson.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
                    return (false, null, msg);
                }

                string? msgId = null;
                if (respJson.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("message_id", out var mid))
                {
                    msgId = mid.GetString();
                }

                return (true, msgId, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Zalo image to {UserId}", userId);
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Upload file media lên Zalo
        /// </summary>
        public async Task<(bool Success, string? Token, string? Error)> UploadMediaAsync(string filePath, string type = "image")
        {
            var token = await EnsureValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
                return (false, null, "No access token");

            if (!File.Exists(filePath))
                return (false, null, "File not found");

            try
            {
                var client = _httpClientFactory.CreateClient();

                using var form = new MultipartFormDataContent();
                using var fileStream = File.OpenRead(filePath);
                using var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                form.Add(fileContent, "file", Path.GetFileName(filePath));

                var response = await client.PostAsync($"{ApiBaseUrl}/oa/message/upload?access_token={token}", form);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return (false, null, error);
                }

                var json = await response.Content.ReadFromJsonAsync<ZaloOaUploadResponse>();
                if (json?.Token == null)
                    return (false, null, "No token returned");

                return (true, json.Token, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading media");
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Upload media từ URL
        /// </summary>
        public async Task<(bool Success, string? Token, string? Error)> UploadMediaFromUrlAsync(string url, string type = "image")
        {
            var token = await EnsureValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
                return (false, null, "No access token");

            try
            {
                var client = _httpClientFactory.CreateClient();

                var body = new { url, type };
                var response = await client.PostAsync(
                    $"{ApiBaseUrl}/oa/message/upload?access_token={token}",
                    new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return (false, null, error);
                }

                var json = await response.Content.ReadFromJsonAsync<ZaloOaUploadResponse>();
                if (json?.Token == null)
                    return (false, null, "No token returned");

                return (true, json.Token, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading media from URL");
                return (false, null, ex.Message);
            }
        }
    }
}
