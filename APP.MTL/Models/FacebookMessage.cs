using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MTL.APP.Models;

/// <summary>
/// Model lưu trữ tin nhắn từ Facebook Fanpage
/// </summary>
[Table("Facebook_Messages")]
public class FacebookMessage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// ID của cuộc trò chuyện (PSID người gửi)
    /// </summary>
    [MaxLength(100)]
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// ID Fanpage nhận tin
    /// </summary>
    [MaxLength(100)]
    public string PageId { get; set; } = string.Empty;

    /// <summary>
    /// Tên người gửi
    /// </summary>
    [MaxLength(250)]
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// Avatar người gửi
    /// </summary>
    [MaxLength(500)]
    public string? SenderAvatar { get; set; }

    /// <summary>
    /// Nội dung tin nhắn
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Loại tin nhắn: text, image, audio, video, file, sticker
    /// </summary>
    [MaxLength(50)]
    public string MessageType { get; set; } = "text";

    /// <summary>
    /// URL đính kèm (ảnh, file)
    /// </summary>
    [MaxLength(500)]
    public string? MediaUrl { get; set; }

    /// <summary>
    /// ID tin nhắn từ Facebook (để tránh trùng lặp)
    /// </summary>
    [MaxLength(100)]
    public string? FacebookMessageId { get; set; }

    /// <summary>
    /// TRUE = tin nhắn từ khách hàng, FALSE = tin nhắn từ Fanpage
    /// </summary>
    public bool IsFromCustomer { get; set; } = true;

    /// <summary>
    /// Thời gian tin nhắn
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Đã đọc chưa
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Đã được xử lý (reply đã gửi) chưa
    /// </summary>
    public bool IsProcessed { get; set; } = false;

    /// <summary>
    /// ID nhân viên đã reply (nếu có)
    /// </summary>
    public int? ReplyByUserId { get; set; }

    /// <summary>
    /// Thời gian reply
    /// </summary>
    public DateTime? ReplyTimestamp { get; set; }

    /// <summary>
    /// Ghi chú nội bộ
    /// </summary>
    public string? InternalNote { get; set; }

    /// <summary>
    /// ID công ty
    /// </summary>
    public int Idcompany { get; set; } = 1;
}

/// <summary>
/// Model cho danh sách cuộc trò chuyện (group by ConversationId)
/// </summary>
public class FacebookConversation
{
    public string ConversationId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatar { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
    public int TotalMessages { get; set; }
}

/// <summary>
/// Model callback từ Facebook Webhook
/// </summary>
public class FacebookWebhookPayload
{
    public string? Object { get; set; }
    public List<FacebookWebhookEntry>? Entry { get; set; }
}

public class FacebookWebhookEntry
{
    public string? Id { get; set; }
    public string? Time { get; set; }
    public List<FacebookWebhookMessaging>? Messaging { get; set; }
}

public class FacebookWebhookMessaging
{
    public FacebookWebhookSender? Sender { get; set; }
    public FacebookWebhookRecipient? Recipient { get; set; }
    public long? Timestamp { get; set; }
    public FacebookWebhookMessage? Message { get; set; }
    public FacebookWebhookDelivery? Delivery { get; set; }
    public FacebookWebhookRead? Read { get; set; }
}

public class FacebookWebhookSender
{
    public string? Id { get; set; }
}

public class FacebookWebhookRecipient
{
    public string? Id { get; set; }
}

public class FacebookWebhookMessage
{
    public string? Mid { get; set; }
    public string? Text { get; set; }
    public List<FacebookWebhookAttachment>? Attachments { get; set; }
    public bool IsEcho { get; set; }
}

public class FacebookWebhookAttachment
{
    public string? Type { get; set; }
    public FacebookWebhookAttachmentPayload? Payload { get; set; }
}

public class FacebookWebhookAttachmentPayload
{
    public string? Url { get; set; }
    public bool? IsReusable { get; set; }
}

public class FacebookWebhookDelivery
{
    public List<string>? Mids { get; set; }
    public string? Watermark { get; set; }
}

public class FacebookWebhookRead
{
    public string? Watermark { get; set; }
}

/// <summary>
/// Model để gửi tin nhắn qua Facebook API
/// </summary>
public class FacebookSendMessageRequest
{
    public string Recipient { get; set; } = string.Empty;
    public FacebookMessageContent? Message { get; set; }
}

public class FacebookMessageContent
{
    public string? Text { get; set; }
    public FacebookAttachment? Attachment { get; set; }
}

public class FacebookAttachment
{
    public string Type { get; set; } = "template";
    public FacebookAttachmentPayload? Payload { get; set; }
}

public class FacebookAttachmentPayload
{
    public string TemplateType { get; set; } = "button";
    public string? Text { get; set; }
    public string? Url { get; set; }
    public bool? IsReusable { get; set; }
    public List<FacebookButton>? Buttons { get; set; }
}

public class FacebookButton
{
    public string Type { get; set; } = "web_url";
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? Payload { get; set; }
}

/// <summary>
/// Model cho Fanpage từ appsettings.json
/// </summary>
public class FanpageInfo
{
    public string FanpageId { get; set; } = string.Empty;
    public string FanpageName { get; set; } = string.Empty;
    public string PageAccessToken { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
}

/// <summary>
/// Cấu hình Facebook từ appsettings.json
/// </summary>
public class FacebookSettingsConfig
{
    public string VerifyToken { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string DefaultPageAccessToken { get; set; } = string.Empty;
    public List<FanpageInfo>? Fanpages { get; set; }
}
