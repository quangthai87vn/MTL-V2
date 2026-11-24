using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.APP.Models;
using MTL.APP.Services;
using Newtonsoft.Json;
using System.Text;

namespace MTL.APP.Controllers;

/// <summary>
/// Controller xử lý Webhook từ Facebook
/// </summary>
[ApiController]
[Route("api/facebook")]
public class FacebookWebhookController : ControllerBase
{
    private readonly MTLContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FacebookWebhookController> _logger;
    private readonly IServiceProvider _serviceProvider;

    public FacebookWebhookController(
        MTLContext context,
        IConfiguration configuration,
        ILogger<FacebookWebhookController> logger,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Xác thực webhook với Facebook (GET request)
    /// Facebook sẽ gọi endpoint này để verify khi setup webhook
    /// </summary>
    [HttpGet("webhook")]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string verifyToken,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        var expectedToken = _configuration["FacebookSettings:VerifyToken"];

        _logger.LogInformation($"Webhook verification - Mode: {mode}, Token match: {verifyToken == expectedToken}");

        if (mode == "subscribe" && verifyToken == expectedToken)
        {
            _logger.LogInformation("Webhook verified successfully");
            return Ok(challenge);
        }

        _logger.LogWarning("Webhook verification failed - Invalid token");
        return Unauthorized("Invalid verification token");
    }

    /// <summary>
    /// Nhận tin nhắn từ Facebook (POST request)
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook()
    {
        try
        {
            // Đọc body của request
            using var reader = new StreamReader(Request.Body);
            var requestBody = await reader.ReadToEndAsync();

            _logger.LogInformation($"Received webhook: {requestBody}");

            // Parse JSON payload
            var payload = JsonConvert.DeserializeObject<FacebookWebhookPayload>(requestBody);

            if (payload == null || payload.Entry == null)
            {
                return Ok("Received");
            }

            // Xử lý từng entry trong payload
            foreach (var entry in payload.Entry)
            {
                if (entry.Messaging != null)
                {
                    foreach (var messaging in entry.Messaging)
                    {
                        await ProcessMessagingEventAsync(messaging);
                    }
                }
            }

            return Ok("Received");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return StatusCode(500, "Error processing webhook");
        }
    }

    /// <summary>
    /// Xử lý từng messaging event
    /// </summary>
    private async Task ProcessMessagingEventAsync(FacebookWebhookMessaging messaging)
    {
        try
        {
            // Bỏ qua nếu là tin nhắn echo (do page gửi)
            if (messaging.Message?.IsEcho == true)
            {
                _logger.LogDebug("Skipping echo message");
                return;
            }

            // Bỏ qua delivery/read acknowledgements
            if (messaging.Delivery != null || messaging.Read != null)
            {
                return;
            }

            if (messaging.Sender == null || messaging.Recipient == null)
            {
                return;
            }

            // Tạo service để xử lý
            using var scope = _serviceProvider.CreateScope();
            var fbService = scope.ServiceProvider.GetRequiredService<FacebookMessageService>();

            // Lấy thông tin người gửi từ Facebook
            var pageAccessToken = GetPageAccessToken(messaging.Recipient.Id);
            string senderName = "Khách hàng";
            string? senderAvatar = null;

            if (!string.IsNullOrEmpty(pageAccessToken))
            {
                var userInfo = await fbService.GetFacebookUserInfoAsync(messaging.Sender.Id, pageAccessToken);
                if (userInfo != null)
                {
                    senderName = userInfo.FullName;
                    senderAvatar = userInfo.ProfilePic;
                }
            }

            // Tạo đối tượng tin nhắn
            var message = new FacebookMessage
            {
                ConversationId = messaging.Sender.Id,
                PageId = messaging.Recipient.Id,
                SenderName = senderName,
                SenderAvatar = senderAvatar,
                FacebookMessageId = messaging.Message?.Mid,
                IsFromCustomer = true,
                Timestamp = messaging.Timestamp.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(messaging.Timestamp.Value).DateTime
                    : DateTime.Now
            };

            // Xử lý nội dung tin nhắn
            if (messaging.Message != null)
            {
                if (!string.IsNullOrEmpty(messaging.Message.Text))
                {
                    message.Message = messaging.Message.Text;
                    message.MessageType = "text";
                }

                // Xử lý attachments
                if (messaging.Message.Attachments != null && messaging.Message.Attachments.Any())
                {
                    var attachment = messaging.Message.Attachments.First();
                    message.MessageType = attachment.Type ?? "file";
                    message.MediaUrl = attachment.Payload?.Url;
                }
            }

            // Lưu vào database
            await fbService.SaveMessageAsync(message);

            _logger.LogInformation($"Saved message from {senderName}: {message.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing messaging event");
        }
    }

    /// <summary>
    /// Lấy Page Access Token từ configuration hoặc database
    /// </summary>
    private string? GetPageAccessToken(string pageId)
    {
        // Ưu tiên lấy từ appsettings.json
        var token = _configuration[$"FacebookSettings:PageAccessTokens:{pageId}"];
        if (!string.IsNullOrEmpty(token))
        {
            return token;
        }

        // Lấy token mặc định
        return _configuration["FacebookSettings:DefaultPageAccessToken"];
    }
}

/// <summary>
/// Controller API để gửi tin nhắn (từ Blazor page)
/// </summary>
[ApiController]
[Route("api/facebook/messages")]
public class FacebookMessageApiController : ControllerBase
{
    private readonly MTLContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FacebookMessageApiController> _logger;

    public FacebookMessageApiController(
        MTLContext context,
        IConfiguration configuration,
        ILogger<FacebookMessageApiController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách cuộc trò chuyện
    /// </summary>
    [HttpGet("conversations/{pageId}")]
    public async Task<ActionResult<List<FacebookConversation>>> GetConversations(string pageId, [FromQuery] int idcompany = 1)
    {
        var conversations = await _context.FacebookMessages
            .Where(m => m.PageId == pageId && m.Idcompany == idcompany)
            .GroupBy(m => m.ConversationId)
            .Select(g => new FacebookConversation
            {
                ConversationId = g.Key,
                SenderName = g.OrderByDescending(x => x.Timestamp).First().SenderName,
                SenderAvatar = g.OrderByDescending(x => x.Timestamp).First().SenderAvatar,
                LastMessage = g.OrderByDescending(x => x.Timestamp).First().Message,
                LastMessageTime = g.Max(x => x.Timestamp),
                UnreadCount = g.Count(x => !x.IsRead && x.IsFromCustomer),
                TotalMessages = g.Count()
            })
            .OrderByDescending(c => c.LastMessageTime)
            .ToListAsync();

        return conversations;
    }

    /// <summary>
    /// Lấy tin nhắn của 1 cuộc trò chuyện
    /// </summary>
    [HttpGet("conversations/{pageId}/{conversationId}")]
    public async Task<ActionResult<List<FacebookMessage>>> GetMessages(
        string pageId,
        string conversationId,
        [FromQuery] int idcompany = 1)
    {
        var messages = await _context.FacebookMessages
            .Where(m => m.ConversationId == conversationId && m.PageId == pageId && m.Idcompany == idcompany)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return messages;
    }

    /// <summary>
    /// Đánh dấu cuộc trò chuyện là đã đọc
    /// </summary>
    [HttpPost("conversations/{pageId}/{conversationId}/read")]
    public async Task<IActionResult> MarkAsRead(string pageId, string conversationId, [FromQuery] int idcompany = 1)
    {
        var unreadMessages = await _context.FacebookMessages
            .Where(m => m.ConversationId == conversationId && m.PageId == pageId && m.Idcompany == idcompany && !m.IsRead)
            .ToListAsync();

        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    /// <summary>
    /// Gửi tin nhắn reply
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            var pageAccessToken = _configuration[$"FacebookSettings:PageAccessTokens:{request.PageId}"];
            if (string.IsNullOrEmpty(pageAccessToken))
            {
                pageAccessToken = _configuration["FacebookSettings:DefaultPageAccessToken"];
            }

            if (string.IsNullOrEmpty(pageAccessToken))
            {
                return BadRequest("Page access token not configured");
            }

            // Gọi Facebook API để gửi tin nhắn
            using var client = new HttpClient();
            client.BaseAddress = new Uri("https://graph.facebook.com/v18.0/");

            var requestBody = new
            {
                recipient = new { id = request.RecipientId },
                message = new { text = request.MessageText }
            };

            var jsonContent = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(
                $"me/messages?access_token={pageAccessToken}",
                jsonContent);

            if (response.IsSuccessStatusCode)
            {
                // Lưu tin nhắn đã gửi vào database
                var sentMessage = new FacebookMessage
                {
                    ConversationId = request.RecipientId,
                    PageId = request.PageId,
                    SenderName = "Admin",
                    Message = request.MessageText,
                    MessageType = "text",
                    IsFromCustomer = false,
                    IsProcessed = true,
                    Idcompany = request.Idcompany
                };

                _context.FacebookMessages.Add(sentMessage);
                await _context.SaveChangesAsync();

                return Ok(sentMessage);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Failed to send message: {errorContent}");
            return StatusCode(500, "Failed to send message");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return StatusCode(500, "Error sending message");
        }
    }

    /// <summary>
    /// Lấy số tin nhắn chưa đọc
    /// </summary>
    [HttpGet("unread-count/{pageId}")]
    public async Task<ActionResult<int>> GetUnreadCount(string pageId, [FromQuery] int idcompany = 1)
    {
        var count = await _context.FacebookMessages
            .CountAsync(m => m.PageId == pageId && m.Idcompany == idcompany && !m.IsRead && m.IsFromCustomer);

        return count;
    }
}

/// <summary>
/// Request model để gửi tin nhắn
/// </summary>
public class SendMessageRequest
{
    public string RecipientId { get; set; } = string.Empty;
    public string PageId { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public int Idcompany { get; set; } = 1;
}
