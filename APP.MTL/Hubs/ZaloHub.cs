using Microsoft.AspNetCore.SignalR;

namespace MTL.APP.Hubs
{
    public class ZaloHub : Hub
    {
        private readonly ILogger<ZaloHub> _logger;

        public ZaloHub(ILogger<ZaloHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected to ZaloHub: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected from ZaloHub: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// N8N gọi endpoint này để gửi tin nhắn real-time đến browser
        /// POST /api/zalo/receive
        /// </summary>
        public async Task SendIncomingMessage(string userId, string message, string messageType,
            string? mediaUrl, string? userName, string? userAvatar)
        {
            _logger.LogInformation("Relaying incoming message from {UserId}", userId);

            await Clients.All.SendAsync("ReceiveZaloMessage", new
            {
                userId,
                message,
                messageType,
                mediaUrl,
                userName,
                userAvatar,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
