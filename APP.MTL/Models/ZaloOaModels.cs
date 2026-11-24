namespace MTL.APP.Models
{
    public class ZaloOaConversation
    {
        public int Id { get; set; }
        public int Idcompany { get; set; }

        /// <summary>Zalo user ID (openid của người dùng)</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Tên hiển thị của người dùng</summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>Avatar URL</summary>
        public string? UserAvatar { get; set; }

        /// <summary>Tin nhắn cuối cùng</summary>
        public string? LastMessage { get; set; }

        /// <summary>Thời gian tin nhắn cuối</summary>
        public DateTime LastMessageTime { get; set; }

        /// <summary>Số tin nhắn chưa đọc</summary>
        public int UnreadCount { get; set; }

        /// <summary>Tổng số tin nhắn</summary>
        public int TotalMessages { get; set; }

        /// <summary>Zalo OA user avatar nhỏ</summary>
        public string? UserThumbAvatar { get; set; }

        /// <summary>Ngày theo dõi</summary>
        public DateTime? FollowerCreatedTime { get; set; }

        /// <summary>Ngày cập nhật cuối</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public class ZaloOaMessage
    {
        public int Id { get; set; }
        public int Idcompany { get; set; }

        /// <summary>Openid của người dùng Zalo</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Tên người dùng (lưu trong message để hiển thị)</summary>
        public string? UserName { get; set; }

        /// <summary>Avatar người dùng</summary>
        public string? UserAvatar { get; set; }

        /// <summary>ID tin nhắn từ Zalo</summary>
        public string? ZaloMessageId { get; set; }

        /// <summary>Nội dung tin nhắn</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Loại tin nhắn: text, image, file, sticker, location, contact, audio, video, link</summary>
        public string MessageType { get; set; } = "text";

        /// <summary>URL media (cho image, file, audio, video)</summary>
        public string? MediaUrl { get; set; }

        /// <summary>Tên file (cho file attachment)</summary>
        public string? FileName { get; set; }

        /// <summary>Kích thước file (bytes)</summary>
        public long? FileSize { get; set; }

        /// <summary>Link trong tin nhắn</summary>
        public string? LinkUrl { get; set; }

        /// <summary>Tiêu đề link</summary>
        public string? LinkTitle { get; set; }

        /// <summary>Mô tả link</summary>
        public string? LinkDescription { get; set; }

        /// <summary>Thumbnail link</summary>
        public string? LinkThumbUrl { get; set; }

        /// <summary>Sticker ID (cho sticker message)</summary>
        public string? StickerId { get; set; }

        /// <summary>Tọa độ latitude (cho location)</summary>
        public double? Latitude { get; set; }

        /// <summary>Tọa độ longitude (cho location)</summary>
        public double? Longitude { get; set; }

        /// <summary>Địa chỉ (cho location)</summary>
        public string? Address { get; set; }

        /// <summary>Có phải tin nhắn từ khách hàng không</summary>
        public bool IsFromCustomer { get; set; } = true;

        /// <summary>Đã đọc chưa</summary>
        public bool IsRead { get; set; }

        /// <summary>Đã xử lý chưa</summary>
        public bool IsProcessed { get; set; }

        /// <summary>Timestamp từ Zalo</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>Thời gian tạo bản ghi</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    #region Zalo OA API Request/Response Models

    public class ZaloOaTokenResponse
    {
        public string? AccessToken { get; set; }
        public long ExpiresIn { get; set; }
        public string? RefreshToken { get; set; }
        public long RefreshTokenExpiresIn { get; set; }
        public string? TokenType { get; set; }
        public int Error { get; set; }
        public string? Message { get; set; }
    }

    public class ZaloOaTokenRefreshResponse
    {
        public string? AccessToken { get; set; }
        public long ExpiresIn { get; set; }
        public string? TokenType { get; set; }
        public int Error { get; set; }
        public string? Message { get; set; }
    }

    public class ZaloOaApiResponse<T>
    {
        public int Error { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    public class ZaloOaFollowersResponse
    {
        public int Total { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
        public List<ZaloOaFollowerItem>? Followers { get; set; }
    }

    public class ZaloOaFollowerItem
    {
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Avatar { get; set; }
        public string? ThumbAvatar { get; set; }
        public string? Gender { get; set; }
        public string? Birthdate { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public long? FollowerCreatedTime { get; set; }
        public long? FollowerUpdatedTime { get; set; }
    }

    public class ZaloOaMessageResponse
    {
        public string? Recipient { get; set; }
        public string? MessageId { get; set; }
    }

    public class ZaloOaUploadResponse
    {
        public string? Token { get; set; }
    }

    #endregion
}
