using Microsoft.EntityFrameworkCore;
using MTL.APP.Models;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace MTL.APP.Services;

/// <summary>
/// Service quản lý tin nhắn Facebook Fanpage
/// </summary>
public class FacebookMessageService
{
    private readonly MTLContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public FacebookMessageService(
        MTLContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    #region Lấy danh sách cuộc trò chuyện

    /// <summary>
    /// Lấy danh sách cuộc trò chuyện theo PageId
    /// </summary>
    public async Task<List<FacebookConversation>> GetConversationsAsync(string pageId, int idcompany)
    {
        var messages = await _context.FacebookMessages
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

        return messages;
    }

    /// <summary>
    /// Lấy danh sách cuộc trò chuyện có tin nhắn chưa đọc
    /// </summary>
    public async Task<List<FacebookConversation>> GetUnreadConversationsAsync(string pageId, int idcompany)
    {
        var messages = await _context.FacebookMessages
            .Where(m => m.PageId == pageId && m.Idcompany == idcompany)
            .Where(m => !m.IsRead && m.IsFromCustomer)
            .GroupBy(m => m.ConversationId)
            .Select(g => new FacebookConversation
            {
                ConversationId = g.Key,
                SenderName = g.OrderByDescending(x => x.Timestamp).First().SenderName,
                SenderAvatar = g.OrderByDescending(x => x.Timestamp).First().SenderAvatar,
                LastMessage = g.OrderByDescending(x => x.Timestamp).First().Message,
                LastMessageTime = g.Max(x => x.Timestamp),
                UnreadCount = g.Count(),
                TotalMessages = g.Count()
            })
            .OrderByDescending(c => c.LastMessageTime)
            .ToListAsync();

        return messages;
    }

    #endregion

    #region Lấy tin nhắn

    /// <summary>
    /// Lấy danh sách tin nhắn theo cuộc trò chuyện
    /// </summary>
    public async Task<List<FacebookMessage>> GetMessagesAsync(string conversationId, string pageId, int idcompany)
    {
        return await _context.FacebookMessages
            .Where(m => m.ConversationId == conversationId && m.PageId == pageId && m.Idcompany == idcompany)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }

    /// <summary>
    /// Lấy tin nhắn mới nhất của mỗi cuộc trò chuyện
    /// </summary>
    public async Task<List<FacebookMessage>> GetLatestMessagesAsync(string pageId, int idcompany, int count = 50)
    {
        return await _context.FacebookMessages
            .Where(m => m.PageId == pageId && m.Idcompany == idcompany)
            .GroupBy(m => m.ConversationId)
            .SelectMany(g => g.OrderByDescending(x => x.Timestamp).Take(1))
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    #endregion

    #region Đánh dấu đã đọc

    /// <summary>
    /// Đánh dấu tất cả tin nhắn trong cuộc trò chuyện là đã đọc
    /// </summary>
    public async Task MarkConversationAsReadAsync(string conversationId, string pageId, int idcompany)
    {
        var unreadMessages = await _context.FacebookMessages
            .Where(m => m.ConversationId == conversationId && m.PageId == pageId && m.Idcompany == idcompany && !m.IsRead)
            .ToListAsync();

        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Đánh dấu tất cả tin nhắn của 1 Page là đã đọc
    /// </summary>
    public async Task MarkAllAsReadAsync(string pageId, int idcompany)
    {
        var unreadMessages = await _context.FacebookMessages
            .Where(m => m.PageId == pageId && m.Idcompany == idcompany && !m.IsRead)
            .ToListAsync();

        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
        }

        await _context.SaveChangesAsync();
    }

    #endregion

    #region Lưu tin nhắn

    /// <summary>
    /// Lưu tin nhắn mới vào database
    /// </summary>
    public async Task<FacebookMessage> SaveMessageAsync(FacebookMessage message)
    {
        // Kiểm tra trùng lặp bằng FacebookMessageId
        if (!string.IsNullOrEmpty(message.FacebookMessageId))
        {
            var existing = await _context.FacebookMessages
                .FirstOrDefaultAsync(m => m.FacebookMessageId == message.FacebookMessageId);
            if (existing != null)
            {
                return existing; // Tin nhắn đã tồn tại, không lưu trùng
            }
        }

        message.Timestamp = DateTime.Now;
        await _context.FacebookMessages.AddAsync(message);
        await _context.SaveChangesAsync();
        return message;
    }

    /// <summary>
    /// Xử lý payload từ webhook và lưu tin nhắn
    /// </summary>
    public async Task ProcessWebhookPayloadAsync(FacebookWebhookPayload payload)
    {
        if (payload?.Entry == null) return;

        foreach (var entry in payload.Entry)
        {
            if (entry.Messaging == null) continue;

            foreach (var messaging in entry.Messaging)
            {
                // Bỏ qua các sự kiện echo (tin nhắn do page gửi)
                if (messaging.Message?.IsEcho == true) continue;

                // Bỏ qua delivery/read acknowledgements
                if (messaging.Delivery != null || messaging.Read != null) continue;

                if (messaging.Sender == null || messaging.Recipient == null) continue;

                var message = new FacebookMessage
                {
                    ConversationId = messaging.Sender.Id,
                    PageId = messaging.Recipient.Id,
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

                await SaveMessageAsync(message);
            }
        }
    }

    #endregion

    #region Gửi tin nhắn qua Facebook API

    /// <summary>
    /// Gửi tin nhắn reply qua Facebook API
    /// </summary>
    public async Task<bool> SendReplyAsync(string recipientId, string messageText, string pageAccessToken)
    {
        try
        {
            var requestBody = new FacebookSendMessageRequest
            {
                Recipient = recipientId,
                Message = new FacebookMessageContent
                {
                    Text = messageText
                }
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://graph.facebook.com/v18.0/");

            var response = await client.PostAsync(
                $"me/messages?access_token={pageAccessToken}",
                httpContent);

            if (response.IsSuccessStatusCode)
            {
                // Lưu tin nhắn đã gửi vào database
                var sentMessage = new FacebookMessage
                {
                    ConversationId = recipientId,
                    PageId = "", // Sẽ được cập nhật
                    Message = messageText,
                    MessageType = "text",
                    IsFromCustomer = false,
                    IsProcessed = true,
                    Timestamp = DateTime.Now
                };

                await SaveMessageAsync(sentMessage);
                return true;
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Gửi tin nhắn với đính kèm (ảnh, file)
    /// </summary>
    public async Task<bool> SendAttachmentAsync(string recipientId, string mediaUrl, string mediaType, string pageAccessToken)
    {
        try
        {
            var requestBody = new FacebookSendMessageRequest
            {
                Recipient = recipientId,
                Message = new FacebookMessageContent
                {
                    Attachment = new FacebookAttachment
                    {
                        Type = mediaType, // image, audio, video, file
                        Payload = new FacebookAttachmentPayload
                        {
                            Url = mediaUrl,
                            IsReusable = true
                        }
                    }
                }
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://graph.facebook.com/v18.0/");

            var response = await client.PostAsync(
                $"me/messages?access_token={pageAccessToken}",
                httpContent);

            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    #endregion

    #region Lấy thông tin người dùng từ Facebook

    /// <summary>
    /// Lấy thông tin người dùng từ Facebook Graph API
    /// </summary>
    public async Task<FacebookUserInfo?> GetFacebookUserInfoAsync(string userId, string pageAccessToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(
                $"https://graph.facebook.com/v18.0/{userId}?fields=first_name,last_name,profile_pic&access_token={pageAccessToken}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<FacebookUserInfo>(json);
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region Thống kê

    /// <summary>
    /// Lấy số tin nhắn chưa đọc của 1 page
    /// </summary>
    public async Task<int> GetUnreadCountAsync(string pageId, int idcompany)
    {
        return await _context.FacebookMessages
            .CountAsync(m => m.PageId == pageId && m.Idcompany == idcompany && !m.IsRead && m.IsFromCustomer);
    }

    /// <summary>
    /// Lấy số cuộc trò chuyện có tin nhắn chưa đọc
    /// </summary>
    public async Task<int> GetUnreadConversationCountAsync(string pageId, int idcompany)
    {
        return await _context.FacebookMessages
            .Where(m => m.PageId == pageId && m.Idcompany == idcompany && !m.IsRead && m.IsFromCustomer)
            .Select(m => m.ConversationId)
            .Distinct()
            .CountAsync();
    }

    #endregion

    #region Xóa tin nhắn

    /// <summary>
    /// Xóa tin nhắn theo ID
    /// </summary>
    public async Task DeleteMessageAsync(int messageId)
    {
        var message = await _context.FacebookMessages.FindAsync(messageId);
        if (message != null)
        {
            _context.FacebookMessages.Remove(message);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Xóa toàn bộ cuộc trò chuyện
    /// </summary>
    public async Task DeleteConversationAsync(string conversationId, string pageId, int idcompany)
    {
        var messages = await _context.FacebookMessages
            .Where(m => m.ConversationId == conversationId && m.PageId == pageId && m.Idcompany == idcompany)
            .ToListAsync();

        _context.FacebookMessages.RemoveRange(messages);
        await _context.SaveChangesAsync();
    }

    #endregion
}

/// <summary>
/// Model thông tin người dùng Facebook
/// </summary>
public class FacebookUserInfo
{
    public string? Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ProfilePic { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
