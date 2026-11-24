using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MTL.APP.Hubs;

namespace MTL.APP.Controllers
{
    [ApiController]
    [Route("api/zalo")]
    public class ZaloApiController : ControllerBase
    {
        private readonly IHubContext<ZaloHub> _hubContext;
        private readonly ILogger<ZaloApiController> _logger;

        public ZaloApiController(
            IHubContext<ZaloHub> hubContext,
            ILogger<ZaloApiController> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// N8N webhook gọi endpoint này khi có tin nhắn mới từ Zalo OA
        /// N8N -> POST /api/zalo/receive
        /// </summary>
        [HttpPost("receive")]
        public async Task<IActionResult> ReceiveMessage([FromBody] ZaloWebhookPayload payload)
        {
            try
            {
                _logger.LogInformation("Received Zalo webhook: Event={Event}, UserId={UserId}",
                    payload.Event, payload.UserId);

                string? userName = null;
                string? userAvatar = null;

                if (payload.Sender != null)
                {
                    userName = payload.Sender.Name;
                }

                var messageType = GetMessageType(payload);
                var message = GetMessageContent(payload);
                var mediaUrl = GetMediaUrl(payload);

                await _hubContext.Clients.All.SendAsync("ReceiveZaloMessage", new
                {
                    userId = payload.UserId ?? "",
                    message,
                    messageType,
                    mediaUrl,
                    userName,
                    userAvatar,
                    timestamp = DateTime.UtcNow
                });

                return Ok(new { success = true, message = "Message received" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Zalo webhook");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "ok", timestamp = DateTime.UtcNow });
        }

        private static string GetMessageType(ZaloWebhookPayload payload)
        {
            return payload.Event?.ToLower() switch
            {
                "user_send_text" => "text",
                "user_send_image" => "image",
                "user_send_file" => "file",
                "user_send_link" => "link",
                "user_send_sticker" => "sticker",
                "user_send_location" => "location",
                "user_send_audio" => "audio",
                "user_send_video" => "video",
                "follow" => "system",
                "unfollow" => "system",
                _ => "text"
            };
        }

        private static string GetMessageContent(ZaloWebhookPayload payload)
        {
            if (payload.Message == null)
                return "";

            if (!string.IsNullOrEmpty(payload.Message.Text))
                return payload.Message.Text;

            return payload.Event?.ToLower() switch
            {
                "user_send_image" => "[Hinh anh]",
                "user_send_file" => "[File]",
                "user_send_link" => payload.Message.Text ?? "[Link]",
                "user_send_sticker" => "[Sticker]",
                "user_send_location" => "[Vi tri]",
                "user_send_audio" => "[Am thanh]",
                "user_send_video" => "[Video]",
                "follow" => "Nguoi dung da theo doi OA",
                "unfollow" => "Nguoi dung da bo theo doi OA",
                _ => payload.Message.Text ?? ""
            };
        }

        private static string? GetMediaUrl(ZaloWebhookPayload payload)
        {
            if (payload.Message?.Attachments == null || !payload.Message.Attachments.Any())
                return null;

            var attachment = payload.Message.Attachments.FirstOrDefault();
            return attachment?.Payload?.Url;
        }
    }

    public class ZaloWebhookPayload
    {
        public string? Event { get; set; }
        public string? UserId { get; set; }
        public ZaloWebhookSender? Sender { get; set; }
        public ZaloWebhookMessage? Message { get; set; }
    }

    public class ZaloWebhookSender
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public class ZaloWebhookMessage
    {
        public string? MsgId { get; set; }
        public string? Text { get; set; }
        public List<ZaloWebhookAttachment>? Attachments { get; set; }
    }

    public class ZaloWebhookAttachment
    {
        public string? Type { get; set; }
        public ZaloWebhookAttachmentPayload? Payload { get; set; }
    }

    public class ZaloWebhookAttachmentPayload
    {
        public string? Url { get; set; }
    }
}
