using MTL.APP.Models;
using System.Net.Http.Json;

namespace MTL.APP.Services
{
    public interface IZaloMessengerService
    {
        Task<List<ZaloConversation>> GetConversationsAsync();
        Task<List<ZaloMessage>> GetMessagesAsync(string conversationId, int page = 1, int pageSize = 10);
        Task<ZaloMessage> SendMessageAsync(string conversationId, string content, string type = "text");
        Task<ZaloMessage> SendImageAsync(string conversationId, string imageBase64, string? fileName = null);
        Task<bool> MarkAsReadAsync(string conversationId);
        Task<int> GetUnreadCountAsync();
        Task<ZaloConversation?> GetOrCreateConversationAsync(string userId);
        Task RefreshMessagesForConversationAsync(string userId);
        void HandleIncomingMessage(string userId, string message, string messageType, string? mediaUrl, string? userName, string? userAvatar);
        bool IsZaloOaConfigured { get; }
        string? ZaloOaStatus { get; }
        string? LastError { get; }
        string? GetDebugAccessToken();
        Task<N8NTokenResult> ForceFetchTokenFromN8NAsync();

        event EventHandler<ZaloMessage>? OnIncomingMessage;
        event EventHandler<ZaloConversation>? OnConversationUpdated;
    }

    public class ZaloMessengerService : IZaloMessengerService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<ZaloMessengerService> _logger;
        private readonly IZaloOaService? _zaloOaService;

        private readonly Dictionary<string, List<ZaloMessage>> _messageCache = new();
        private readonly Dictionary<string, ZaloConversation> _conversationCache = new();
        private string? _lastError;

        public string? LastError => _lastError;

        public event EventHandler<ZaloMessage>? OnIncomingMessage;
        public event EventHandler<ZaloConversation>? OnConversationUpdated;

        public ZaloMessengerService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<ZaloMessengerService> logger,
            IServiceProvider serviceProvider)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;

            try
            {
                _zaloOaService = serviceProvider.GetService<IZaloOaService>();
            }
            catch
            {
                _zaloOaService = null;
            }
        }

        public bool IsZaloOaConfigured => _zaloOaService?.IsConfigured ?? false;

        public string? ZaloOaStatus
        {
            get
            {
                if (_zaloOaService == null)
                    return "Service not available";

                if (!_zaloOaService.IsConfigured)
                    return "Not configured - Add ZaloSettings in appsettings.json";

                var token = _zaloOaService.GetAccessToken();
                if (string.IsNullOrEmpty(token))
                    return "No access token - Check AppId/AppSecret in appsettings.json";

                return "Connected";
            }
        }

        /// <summary>
        /// Lấy danh sách cuộc trò chuyện từ Zalo OA API
        /// Endpoint: GET /v2.0/me/conversation
        /// </summary>
        public async Task<List<ZaloConversation>> GetConversationsAsync()
        {
            _lastError = null;

            if (_zaloOaService?.IsConfigured != true)
            {
                _lastError = "Zalo OA chua duoc cau hinh. Vui long kiem tra ZaloSettings trong appsettings.json (AppId, AppSecret).";
                _logger.LogError(_lastError);
                throw new InvalidOperationException(_lastError);
            }

            try
            {
                var conversations = await _zaloOaService.GetConversationsAsync(10);

                if (conversations == null || !conversations.Any())
                {
                    _lastError = "Khong co cuoc tro chuyen nao. Dam bao da co nguoi dung nhan tin den Zalo OA.";
                    _logger.LogWarning(_lastError);
                    throw new InvalidOperationException(_lastError);
                }

                var result = conversations.Select(c => new ZaloConversation
                {
                    Id = c.UserId ?? "",
                    Name = c.UserName ?? "Khach hang",
                    AvatarUrl = c.UserAvatar ?? "",
                    LastMessage = c.LastMessage ?? "",
                    Time = c.LastMessageTime != default ? c.LastMessageTime.ToString("HH:mm") : "",
                    LastMessageTime = c.LastMessageTime,
                    UnreadCount = c.UnreadCount,
                    TotalMessages = c.TotalMessages,
                    IsOnline = false
                }).ToList();

                foreach (var conv in result)
                {
                    _conversationCache[conv.Id] = conv;
                }

                return result;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _lastError = $"Loi khi lay danh sach cuoc tro chuyen: {ex.Message}";
                _logger.LogError(ex, "Error getting Zalo OA conversations");
                throw new InvalidOperationException(_lastError, ex);
            }
        }

        /// <summary>
        /// Lấy tin nhắn của một user từ Zalo OA API
        /// Endpoint: GET /v2.0/me/messages
        /// </summary>
        public async Task<List<ZaloMessage>> GetMessagesAsync(string conversationId, int page = 1, int pageSize = 10)
        {
            _lastError = null;

            if (_zaloOaService?.IsConfigured != true)
            {
                _lastError = "Zalo OA chua duoc cau hinh.";
                throw new InvalidOperationException(_lastError);
            }

            try
            {
                var offset = (page - 1) * pageSize;
                var messages = await _zaloOaService.GetMessagesAsync(conversationId, offset, pageSize);

                var result = messages.Select(m => new ZaloMessage
                {
                    Id = m.ZaloMessageId ?? Guid.NewGuid().ToString(),
                    Content = m.Message ?? "",
                    Type = m.MessageType ?? "text",
                    FileUrl = m.MediaUrl,
                    IsFromUser = m.IsFromCustomer,
                    Time = m.Timestamp != default ? m.Timestamp.ToString("HH:mm") : "",
                    CreatedAt = m.Timestamp != default ? m.Timestamp : DateTime.Now,
                    Status = m.IsRead ? "read" : "delivered",
                    IsRead = m.IsRead
                }).ToList();

                if (_messageCache.ContainsKey(conversationId))
                {
                    var existingIds = _messageCache[conversationId].Select(x => x.Id).ToHashSet();
                    foreach (var msg in result)
                    {
                        if (!existingIds.Contains(msg.Id))
                        {
                            _messageCache[conversationId].Add(msg);
                        }
                    }
                }
                else
                {
                    _messageCache[conversationId] = result;
                }

                return _messageCache[conversationId].OrderBy(x => x.CreatedAt).ToList();
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _lastError = $"Loi khi lay tin nhan: {ex.Message}";
                _logger.LogError(ex, "Error getting Zalo OA messages for {UserId}", conversationId);
                throw new InvalidOperationException(_lastError, ex);
            }
        }

        /// <summary>
        /// Lấy chi tiết conversation cho một user
        /// </summary>
        public async Task<ZaloConversation?> GetOrCreateConversationAsync(string userId)
        {
            if (_conversationCache.TryGetValue(userId, out var cached))
            {
                return cached;
            }

            if (_zaloOaService?.IsConfigured == true)
            {
                try
                {
                    var messages = await _zaloOaService.GetMessagesAsync(userId, 0, 10);

                    var conv = new ZaloConversation
                    {
                        Id = userId,
                        Name = "Khach hang",
                        Messages = new List<ZaloMessage>()
                    };

                    _conversationCache[userId] = conv;
                    return conv;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting conversation for user {UserId}", userId);
                }
            }

            return new ZaloConversation { Id = userId, Name = "Khach hang" };
        }

        public Task RefreshMessagesForConversationAsync(string userId)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gửi tin nhắn đến user qua Zalo OA API
        /// </summary>
        public async Task<ZaloMessage> SendMessageAsync(string conversationId, string content, string type = "text")
        {
            if (_zaloOaService?.IsConfigured != true)
            {
                throw new InvalidOperationException("Zalo OA chua duoc cau hinh.");
            }

            try
            {
                var (success, messageId, error) = await _zaloOaService.SendTextMessageAsync(conversationId, content);

                var msg = new ZaloMessage
                {
                    Id = messageId ?? Guid.NewGuid().ToString(),
                    Content = content,
                    Type = type,
                    IsFromUser = false,
                    Time = DateTime.Now.ToString("HH:mm"),
                    CreatedAt = DateTime.Now,
                    Status = success ? "sent" : "failed"
                };

                if (success)
                {
                    AddMessageToCache(conversationId, msg);
                    UpdateConversationLastMessage(conversationId, content);
                }
                else
                {
                    throw new InvalidOperationException(error ?? "Gui tin nhan that bai");
                }

                return msg;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Zalo OA message");
                throw new InvalidOperationException($"Loi gui tin nhan: {ex.Message}", ex);
            }
        }

        public async Task<ZaloMessage> SendImageAsync(string conversationId, string imageBase64, string? fileName = null)
        {
            if (_zaloOaService?.IsConfigured != true)
            {
                throw new InvalidOperationException("Zalo OA chua duoc cau hinh.");
            }

            try
            {
                var (uploaded, token, _) = await _zaloOaService.UploadMediaFromUrlAsync(imageBase64, "image");
                if (uploaded && !string.IsNullOrEmpty(token))
                {
                    var (success, messageId, _) = await _zaloOaService.SendImageMessageAsync(conversationId, token);

                    var msg = new ZaloMessage
                    {
                        Id = messageId ?? Guid.NewGuid().ToString(),
                        Content = imageBase64,
                        Type = "image",
                        FileName = fileName,
                        IsFromUser = false,
                        Time = DateTime.Now.ToString("HH:mm"),
                        CreatedAt = DateTime.Now,
                        Status = success ? "sent" : "failed"
                    };

                    if (success)
                    {
                        AddMessageToCache(conversationId, msg);
                    }

                    return msg;
                }

                throw new InvalidOperationException("Upload hinh anh that bai");
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Zalo OA image");
                throw new InvalidOperationException($"Loi gui hinh anh: {ex.Message}", ex);
            }
        }

        public Task<bool> MarkAsReadAsync(string conversationId)
        {
            if (_conversationCache.TryGetValue(conversationId, out var conv))
            {
                conv.UnreadCount = 0;
            }
            return Task.FromResult(true);
        }

        public Task<int> GetUnreadCountAsync()
        {
            var total = _conversationCache.Values.Sum(c => c.UnreadCount);
            return Task.FromResult(total);
        }

        /// <summary>
        /// Xử lý tin nhắn đến từ N8N webhook
        /// </summary>
        public void HandleIncomingMessage(string userId, string message, string messageType, string? mediaUrl,
            string? userName, string? userAvatar)
        {
            var msg = new ZaloMessage
            {
                Id = Guid.NewGuid().ToString(),
                Content = message,
                Type = messageType,
                FileUrl = mediaUrl,
                IsFromUser = true,
                Time = DateTime.Now.ToString("HH:mm"),
                CreatedAt = DateTime.Now,
                Status = "received"
            };

            AddMessageToCache(userId, msg);
            UpdateConversationLastMessage(userId, message);

            if (!_conversationCache.TryGetValue(userId, out var conv))
            {
                conv = new ZaloConversation
                {
                    Id = userId,
                    Name = userName ?? "Khach hang",
                    AvatarUrl = userAvatar,
                    UnreadCount = 0,
                    Messages = new List<ZaloMessage>()
                };
                _conversationCache[userId] = conv;
            }

            conv.UnreadCount++;
            conv.LastMessage = message;
            conv.Time = msg.Time;

            OnIncomingMessage?.Invoke(this, msg);
            OnConversationUpdated?.Invoke(this, conv);
        }

        private void AddMessageToCache(string conversationId, ZaloMessage msg)
        {
            if (!_messageCache.ContainsKey(conversationId))
            {
                _messageCache[conversationId] = new List<ZaloMessage>();
            }
            _messageCache[conversationId].Add(msg);
        }

        private void UpdateConversationLastMessage(string conversationId, string message)
        {
            if (_conversationCache.TryGetValue(conversationId, out var conv))
            {
                conv.LastMessage = message;
                conv.Time = DateTime.Now.ToString("HH:mm");
            }
        }

        public string? GetDebugAccessToken() => _zaloOaService?.GetDebugAccessToken();

        public async Task<N8NTokenResult> ForceFetchTokenFromN8NAsync()
        {
            if (_zaloOaService is null)
                return new N8NTokenResult(false, null, null, null, "ZaloOaService not available", null, false);

            return await _zaloOaService.ForceFetchTokenFromN8NAsync();
        }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T Data { get; set; } = default!;
    }
}
