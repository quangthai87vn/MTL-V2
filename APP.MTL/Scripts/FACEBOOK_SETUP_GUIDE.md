# Hướng dẫn cấu hình Facebook Messenger Webhook

## 1. Tạo Facebook App

1. Truy cập [Facebook Developer Console](https://developers.facebook.com/)
2. Đăng nhập tài khoản Facebook của bạn
3. Click **"My Apps"** → **"Create App"**
4. Chọn loại app: **"Business"**
5. Điền thông tin:
   - Tên app: `MTL CRM Facebook Integration`
   - Email liên hệ: email của bạn
6. Click **"Create app"**

## 2. Thêm sản phẩm Messenger

1. Trong Dashboard của app, tìm mục **"Add products"**
2. Click **"Set up"** bên dưới **Messenger**
3. Sau khi setup, bạn sẽ thấy các cài đặt Messenger

## 3. Lấy Page Access Token

1. Trong phần **"Messenger Settings"**, tìm mục **"Token Generation"**
2. Chọn Facebook Page mà bạn muốn kết nối
3. Click **"Generate Token"**
4. **QUAN TRỌNG**: Copy và lưu token này lại (sẽ dùng trong appsettings.json)

## 4. Cấu hình Webhook

1. Trong phần **"Webhooks"**, click **"Setup Webhooks"**
2. Điền thông tin:
   - **Callback URL**: `https://your-domain.com/api/facebook/webhook`
     - Thay `your-domain.com` bằng domain thật của bạn
     - **Lưu ý**: Phải sử dụng HTTPS
   - **Verify Token**: Một chuỗi text bất kỳ bạn tự định nghĩa
     - VD: `MySecretVerifyToken123`
   - **Subscription Fields**: Chọn các fields:
     - ✅ `messages`
     - ✅ `messaging_postbacks`
     - ✅ `messaging_optins`
     - ✅ `messaging_deliveries`
     - ✅ `messaging_reads`
3. Click **"Verify and Save"**

## 5. Cấu hình ứng dụng

### 5.1 Cập nhật appsettings.json

```json
{
  "FacebookSettings": {
    "VerifyToken": "YOUR_VERIFY_TOKEN",
    "AppSecret": "YOUR_APP_SECRET",
    "DefaultPageAccessToken": "YOUR_PAGE_ACCESS_TOKEN",
    "PageAccessTokens": {
      "YOUR_PAGE_ID_1": "YOUR_PAGE_ACCESS_TOKEN_1"
    }
  }
}
```

### 5.2 Cập nhật trang TinNhanFacebook.razor

Trong file `Pages/TinNhan/TinNhanFacebook.razor`, cập nhật method `LoadFanpages()`:

```csharp
private async Task LoadFanpages()
{
    // TODO: Load từ database hoặc config
    // Cập nhật thông tin thật của bạn
    Fanpages = new List<FanpageInfo>
    {
        new FanpageInfo { FanpageId = "YOUR_REAL_PAGE_ID", FanpageName = "Tên Fanpage của bạn", UnreadCount = 0 },
    };
}
```

## 6. Chạy Script SQL

Chạy file `Scripts/CreateFacebookMessagesTable.sql` trên SQL Server để tạo bảng lưu trữ tin nhắn.

## 7. Cấp quyền cần thiết

Đảm bảo app có các quyền sau:
- `pages_messaging`
- `pages_messaging_subscriptions`
- `pages_messaging_payments`
- `pages_messaging_phone_number`
- `pages_messaging_group_labels`

## 8. Test Webhook

### Test Verify:
```bash
curl -X GET "https://your-domain.com/api/facebook/webhook?hub.mode=subscribe&hub.verify_token=YOUR_VERIFY_TOKEN&hub.challenge=challenge_string"
```

### Test nhận tin nhắn:
Gửi một tin nhắn test từ tài khoản Facebook đến Fanpage đã kết nối. Kiểm tra:
1. Database có bản ghi mới trong bảng `Facebook_Messages`
2. Trang `/tinnhan/facebook` hiển thị cuộc trò chuyện

## 9. Troubleshooting

### Lỗi "Webhook verification failed"
- Kiểm tra VerifyToken trong appsettings.json khớp với Verify Token đã nhập trên Facebook Developer
- Đảm bảo URL webhook đúng và có thể truy cập từ internet

### Không nhận được tin nhắn
- Kiểm tra app đã được publish lên server chưa
- Kiểm tra Page Access Token còn hiệu lực không
- Kiểm tra log của ứng dụng để xem có lỗi gì không

### Lỗi CORS
- Đảm bảo API endpoint được gọi từ cùng domain

## 10. Các bước tiếp theo (Optional)

### Thêm nhiều Fanpage:
Cập nhật `PageAccessTokens` trong appsettings.json:
```json
"PageAccessTokens": {
  "PAGE_ID_1": "TOKEN_1",
  "PAGE_ID_2": "TOKEN_2",
  "PAGE_ID_3": "TOKEN_3"
}
```

### Auto-reply (Chatbot):
Có thể thêm logic auto-reply trong `ProcessWebhookPayloadAsync` của `FacebookWebhookController.cs`

### Gửi hình ảnh/đính kèm:
Sử dụng method `SendAttachmentAsync` trong `FacebookMessageService.cs`
