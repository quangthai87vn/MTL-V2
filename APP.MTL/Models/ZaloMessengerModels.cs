namespace MTL.APP.Models
{
    public class ZaloConversation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string LastMessage { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public DateTime LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
        public bool IsOnline { get; set; }
        public int TotalMessages { get; set; }
        public List<ZaloMessage> Messages { get; set; } = new();
    }

    public class ZaloMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = "text"; // text, image, file
        public string? FileName { get; set; }
        public string? FileUrl { get; set; }
        public long? FileSize { get; set; } // in bytes
        public string? MimeType { get; set; }
        public bool IsFromUser { get; set; }
        public string Time { get; set; } = DateTime.Now.ToString("HH:mm");
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Status { get; set; } = "sent"; // sent, delivered, read
        public bool IsRead { get; set; }
    }

    public class MessageSearchResult
    {
        public List<ZaloMessage> Messages { get; set; } = new();
        public int TotalCount { get; set; }
        public string? ConversationName { get; set; }
    }

    public class PaginatedMessages
    {
        public List<ZaloMessage> Messages { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
    }
}
