# MTL Media Bridge

Plugin nhỏ cho WordPress/WooCommerce, bổ sung Media Library API tại
`/wp-json/wc-mtl/v1/media`. WooCommerce Consumer Key/Secret hiện có được dùng
để xác thực, vì vậy MTL.V2 không cần lưu thêm mật khẩu quản trị WordPress.

## Cài đặt

1. Nén thư mục `mtl-media-bridge` thành `mtl-media-bridge.zip`.
2. Trong WordPress vào **Plugin → Cài plugin → Tải plugin lên**.
3. Cài đặt và kích hoạt **MTL Media Bridge**.
4. WooCommerce REST API key phải có quyền **Read/Write** và thuộc người dùng có quyền `upload_files`.

Sau khi kích hoạt, hộp Media của MTL.V2 tự chuyển từ dữ liệu ảnh cache trong
PostgreSQL sang Media Library trực tiếp của WordPress và bật chức năng upload.
