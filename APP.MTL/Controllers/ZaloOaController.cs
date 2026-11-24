using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using MTL.APP.Models;
using MTL.APP.Services;
using MTL.APP.Hubs;
using System.Text.Json;

namespace MTL.APP.Controllers
{
    /// <summary>
    /// Controller nhận webhook events từ Zalo OA
    /// Đăng ký URL này trong Zalo Developer Console: https://yourdomain.com/api/zalo/webhook
    /// </summary>
    [ApiController]
    [Route("api/zalo")]
    public class ZaloOaWebhookController : ControllerBase
    {
        private readonly IDbContextFactory<MTLContext> _contextFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ZaloOaWebhookController> _logger;
        private readonly IHubContext<ZaloHub> _hubContext;
        private readonly IZaloOaService _zaloOaService;

        public ZaloOaWebhookController(
            IDbContextFactory<MTLContext> contextFactory,
            IConfiguration configuration,
            ILogger<ZaloOaWebhookController> logger,
            IHubContext<ZaloHub> hubContext,
            IZaloOaService zaloOaService)
        {
            _contextFactory = contextFactory;
            _configuration = configuration;
            _logger = logger;
            _hubContext = hubContext;
            _zaloOaService = zaloOaService;
        }

        /// <summary>
        /// Xác thực webhook với Zalo (GET request khi đăng ký webhook)
        /// </summary>
        [HttpGet("webhook")]
        public IActionResult VerifyWebhook(
            [FromQuery] long? timestamp,
            [FromQuery] string? msg_signature,
            [FromQuery] string? challenge)
        {
            var verifyToken = _configuration["ZaloSettings:WebhookVerifyToken"];
            var expectedChallenge = _configuration["ZaloSettings:WebhookChallenge"];

            _logger.LogInformation("Zalo webhook verification - timestamp: {Ts}, challenge: {Challenge}", timestamp, challenge);

            if (!string.IsNullOrEmpty(expectedChallenge) && challenge == expectedChallenge)
            {
                _logger.LogInformation("Zalo webhook verified successfully");
                return Ok(challenge);
            }

            if (string.IsNullOrEmpty(verifyToken))
            {
                _logger.LogInformation("Zalo webhook verification - no verify token configured, auto-accepting");
                return Ok(challenge);
            }

            _logger.LogWarning("Zalo webhook verification failed");
            return Unauthorized();
        }

        /// <summary>
        /// Nhận sự kiện từ Zalo (POST request)
        /// Chuyển tiếp đến browser qua SignalR
        /// </summary>
        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveWebhook([FromBody] JsonElement payload)
        {
            try
            {
                var json = payload.GetRawText();
                _logger.LogInformation("Received Zalo webhook: {Payload}", json.Length > 500 ? json[..500] + "..." : json);

                var eventType = payload.TryGetProperty("event_name", out var et) ? et.GetString() : null;
                var userId = payload.TryGetProperty("sender", out var s) && s.TryGetProperty("id", out var sid) ? sid.GetString() : null;

                if (string.IsNullOrEmpty(eventType))
                {
                    _logger.LogWarning("Webhook missing event_name");
                    return Ok(new { status = "received" });
                }

                _logger.LogInformation("Zalo event: {EventType} from user: {UserId}", eventType, userId);

                string? userName = null;
                string? userAvatar = null;
                string messageType = "text";
                string messageText = "";
                string? mediaUrl = null;

                var sender = payload.TryGetProperty("sender", out var senderData) ? senderData : default;
                userName = sender.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;

                var message = payload.TryGetProperty("message", out var m) ? m : default;

                messageType = eventType?.ToLower() switch
                {
                    "user_send_text" => "text",
                    "user_send_image" => "image",
                    "user_send_file" => "file",
                    "user_send_link" => "link",
                    "user_send_sticker" => "sticker",
                    "user_send_location" => "location",
                    "follow" => "system",
                    "unfollow" => "system",
                    _ => "text"
                };

                switch (eventType?.ToLower())
                {
                    case "user_send_text":
                        messageText = message.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                        break;
                    case "user_send_image":
                        messageText = "[Hinh anh]";
                        if (message.TryGetProperty("attachments", out var atts) && atts.ValueKind == JsonValueKind.Array)
                        {
                            var arr = atts.EnumerateArray().FirstOrDefault();
                            if (arr.TryGetProperty("payload", out var p) && p.TryGetProperty("url", out var url))
                                mediaUrl = url.GetString();
                        }
                        break;
                    case "user_send_file":
                        messageText = "[File]";
                        if (message.TryGetProperty("attachments", out var fileAtts) && fileAtts.ValueKind == JsonValueKind.Array)
                        {
                            var arr = fileAtts.EnumerateArray().FirstOrDefault();
                            if (arr.TryGetProperty("payload", out var p) && p.TryGetProperty("url", out var url))
                                mediaUrl = url.GetString();
                        }
                        break;
                    case "user_send_link":
                        messageText = message.TryGetProperty("text", out var lt) ? lt.GetString() ?? "" : "";
                        break;
                    case "user_send_sticker":
                        messageText = "[Sticker]";
                        break;
                    case "user_send_location":
                        messageText = "[Vi tri]";
                        break;
                    case "follow":
                        messageText = "Nguoi dung da theo doi OA";
                        break;
                    case "unfollow":
                        messageText = "Nguoi dung da bo theo doi OA";
                        break;
                    default:
                        messageText = message.TryGetProperty("text", out var ot) ? ot.GetString() ?? "" : "";
                        break;
                }

                if (!string.IsNullOrEmpty(userId))
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveZaloMessage", new
                    {
                        userId,
                        message = messageText,
                        messageType,
                        mediaUrl,
                        userName = userName ?? "Khach hang",
                        userAvatar = userAvatar ?? "",
                        timestamp = DateTime.UtcNow
                    });
                }

                return Ok(new { status = "received" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Zalo webhook");
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }
    }

    /// <summary>
    /// API Controller cho Zalo OA Messenger (Blazor page gọi các endpoint này)
    /// </summary>
    [ApiController]
    [Route("api/zalo/messages")]
    public class ZaloOaApiController : ControllerBase
    {
        private readonly IDbContextFactory<MTLContext> _contextFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ZaloOaApiController> _logger;

        public ZaloOaApiController(
            IDbContextFactory<MTLContext> contextFactory,
            IConfiguration configuration,
            ILogger<ZaloOaApiController> logger)
        {
            _contextFactory = contextFactory;
            _configuration = configuration;
            _logger = logger;
        }

        private int GetCompanyId() =>
            int.TryParse(_configuration["MTLSettings:Idcompany"], out var v) ? v : 1;

        /// <summary>Lấy danh sách cuộc trò chuyện</summary>
        [HttpGet("conversations")]
        public async Task<ActionResult<List<ZaloOaConversation>>> GetConversations([FromQuery] int? idcompany = null)
        {
            var companyId = idcompany ?? GetCompanyId();
            var ctx = await _contextFactory.CreateDbContextAsync();

            var conversations = await ctx.ZaloOaMessages
                .AsNoTracking()
                .Where(m => m.Idcompany == companyId)
                .GroupBy(m => m.UserId)
                .Select(g => new ZaloOaConversation
                {
                    Idcompany = companyId,
                    UserId = g.Key,
                    UserName = g.OrderByDescending(x => x.Timestamp).First().UserName ?? "Khach hang",
                    UserAvatar = g.OrderByDescending(x => x.Timestamp).First().UserAvatar,
                    UserThumbAvatar = g.OrderByDescending(x => x.Timestamp).First().UserAvatar,
                    LastMessage = g.OrderByDescending(x => x.Timestamp).First().Message,
                    LastMessageTime = g.Max(x => x.Timestamp),
                    UnreadCount = g.Count(x => !x.IsRead && x.IsFromCustomer),
                    TotalMessages = g.Count()
                })
                .OrderByDescending(c => c.LastMessageTime)
                .ToListAsync();

            return conversations;
        }

        /// <summary>Lấy tin nhắn của 1 cuộc trò chuyện</summary>
        [HttpGet("conversations/{userId}")]
        public async Task<ActionResult<List<ZaloOaMessage>>> GetMessages(
            string userId,
            [FromQuery] int? idcompany = null,
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 50)
        {
            var companyId = idcompany ?? GetCompanyId();
            var ctx = await _contextFactory.CreateDbContextAsync();

            var messages = await ctx.ZaloOaMessages
                .AsNoTracking()
                .Where(m => m.UserId == userId && m.Idcompany == companyId)
                .OrderByDescending(m => m.Timestamp)
                .Skip(offset)
                .Take(limit)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            return messages;
        }

        /// <summary>Đánh dấu cuộc trò chuyện là đã đọc</summary>
        [HttpPost("conversations/{userId}/read")]
        public async Task<IActionResult> MarkAsRead(string userId, [FromQuery] int? idcompany = null)
        {
            var companyId = idcompany ?? GetCompanyId();
            var ctx = await _contextFactory.CreateDbContextAsync();

            var unread = await ctx.ZaloOaMessages
                .Where(m => m.UserId == userId && m.Idcompany == companyId && !m.IsRead && m.IsFromCustomer)
                .ToListAsync();

            foreach (var msg in unread)
                msg.IsRead = true;

            await ctx.SaveChangesAsync();
            return Ok(new { markedRead = unread.Count });
        }

        /// <summary>Gửi tin nhắn text đến người dùng Zalo</summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] ZaloSendMessageRequest request)
        {
            try
            {
                var scope = HttpContext.RequestServices.CreateScope();
                var zaloService = scope.ServiceProvider.GetRequiredService<IZaloOaService>();

                if (!zaloService.IsConfigured)
                    return BadRequest(new { error = "Zalo OA is not configured" });

                if (string.IsNullOrWhiteSpace(request.UserId))
                    return BadRequest(new { error = "UserId is required" });

                if (string.IsNullOrWhiteSpace(request.MessageText))
                    return BadRequest(new { error = "MessageText is required" });

                var (success, messageId, error) = await zaloService.SendTextMessageAsync(request.UserId, request.MessageText);

                if (!success)
                {
                    _logger.LogError("Failed to send Zalo message: {Error}", error);
                    return StatusCode(500, new { error = error ?? "Failed to send message" });
                }

                return Ok(new { success = true, messageId = messageId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Zalo message");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>Gửi tin nhắn image đến người dùng Zalo</summary>
        [HttpPost("send-image")]
        public async Task<IActionResult> SendImageMessage([FromBody] ZaloSendImageRequest request)
        {
            try
            {
                var scope = HttpContext.RequestServices.CreateScope();
                var zaloService = scope.ServiceProvider.GetRequiredService<IZaloOaService>();

                if (!zaloService.IsConfigured)
                    return BadRequest(new { error = "Zalo OA is not configured" });

                if (string.IsNullOrWhiteSpace(request.UserId))
                    return BadRequest(new { error = "UserId is required" });

                if (string.IsNullOrWhiteSpace(request.ImageToken))
                    return BadRequest(new { error = "ImageToken is required" });

                var (success, messageId, error) = await zaloService.SendImageMessageAsync(request.UserId, request.ImageToken);

                if (!success)
                    return StatusCode(500, new { error = error });

                return Ok(new { success = true, messageId = messageId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Zalo image");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>Upload file để lấy token gửi tin nhắn</summary>
        [HttpPost("upload")]
        public async Task<IActionResult> UploadMedia(IFormFile file, [FromQuery] string type = "image")
        {
            try
            {
                var scope = HttpContext.RequestServices.CreateScope();
                var zaloService = scope.ServiceProvider.GetRequiredService<IZaloOaService>();

                if (!zaloService.IsConfigured)
                    return BadRequest(new { error = "Zalo OA is not configured" });

                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "No file provided" });

                var tempPath = Path.Combine(Path.GetTempPath(), $"zalo_upload_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");
                await using (var stream = System.IO.File.Create(tempPath))
                    await file.CopyToAsync(stream);

                var (success, token, error) = await zaloService.UploadMediaAsync(tempPath, type);

                System.IO.File.Delete(tempPath);

                if (!success)
                    return StatusCode(500, new { error = error });

                return Ok(new { success = true, token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading Zalo media");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>Lấy số tin nhắn chưa đọc</summary>
        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount([FromQuery] int? idcompany = null)
        {
            var companyId = idcompany ?? GetCompanyId();
            var ctx = await _contextFactory.CreateDbContextAsync();
            return await ctx.ZaloOaMessages
                .CountAsync(m => m.Idcompany == companyId && !m.IsRead && m.IsFromCustomer);
        }

        /// <summary>Lấy thông tin OA</summary>
        [HttpGet("oa-info")]
        public async Task<IActionResult> GetOaInfo()
        {
            var scope = HttpContext.RequestServices.CreateScope();
            var zaloService = scope.ServiceProvider.GetRequiredService<IZaloOaService>();

            if (!zaloService.IsConfigured)
                return BadRequest(new { error = "Zalo OA is not configured" });

            var info = await zaloService.GetOaInfoAsync();
            if (info == null)
                return StatusCode(500, new { error = "Failed to get OA info" });

            return Ok(new
            {
                isConfigured = true,
                hasValidToken = !string.IsNullOrEmpty(zaloService.GetAccessToken()),
                oaName = info.UserName,
                oaAvatar = info.Avatar,
                oaId = info.UserId
            });
        }

        /// <summary>
        /// API test để thêm tin nhắn mẫu vào database
        /// </summary>
        [HttpPost("test/add-message")]
        public async Task<IActionResult> AddTestMessage([FromBody] ZaloAddTestMessageRequest request)
        {
            try
            {
                var ctx = await _contextFactory.CreateDbContextAsync();
                var message = new ZaloOaMessage
                {
                    Idcompany = request.Idcompany,
                    UserId = request.UserId,
                    UserName = request.UserName ?? "Khach hang test",
                    UserAvatar = $"https://api.dicebear.com/7.x/initials/svg?seed={request.UserId}",
                    ZaloMessageId = Guid.NewGuid().ToString(),
                    Message = request.Message,
                    MessageType = "text",
                    IsFromCustomer = request.IsFromCustomer,
                    IsRead = false,
                    Timestamp = DateTime.UtcNow.AddMinutes(-request.MinutesAgo),
                    CreatedAt = DateTime.UtcNow.AddMinutes(-request.MinutesAgo)
                };

                ctx.ZaloOaMessages.Add(message);
                await ctx.SaveChangesAsync();

                return Ok(new { success = true, message = "Test message added", id = message.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add test message");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class ZaloAddTestMessageRequest
    {
        public int Idcompany { get; set; } = 1;
        public string UserId { get; set; } = "test_user_1";
        public string? UserName { get; set; }
        public string Message { get; set; } = "Tin nhan test";
        public bool IsFromCustomer { get; set; } = true;
        public int MinutesAgo { get; set; } = 0;
    }

    public class ZaloSendMessageRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string MessageText { get; set; } = string.Empty;
        public int Idcompany { get; set; } = 1;
    }

    public class ZaloSendImageRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string ImageToken { get; set; } = string.Empty;
    }
}
