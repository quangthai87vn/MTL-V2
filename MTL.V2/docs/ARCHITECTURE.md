# Kiến trúc MTL V2

## Quyết định chính

MTL V2 dùng **modular monolith**. Các module chạy chung một API và một cơ sở dữ liệu nhưng có ranh giới rõ ràng. Cách này phù hợp quy mô doanh nghiệp hiện tại, dễ sao lưu và vận hành hơn microservice, đồng thời vẫn có thể tách module đồng bộ hoặc báo cáo khi tải tăng.

```text
Browser
  └─ React admin portal
       └─ ASP.NET Core API
            ├─ Identity & permissions
            ├─ Catalog & WooCommerce sync
            ├─ Sales
            ├─ Purchasing
            ├─ CRM
            ├─ Inventory
            └─ Settings & audit
                 └─ PostgreSQL
```

## Bảo mật

- Cookie đăng nhập HttpOnly, SameSite Strict; không lưu access token trong localStorage.
- Mọi API mặc định yêu cầu đăng nhập; chỉ health check, login và webhook đã xác minh được anonymous.
- Phân quyền theo permission (`products.read`, `sales.manage`, …), không chỉ ẩn menu.
- Company ID lấy từ claims phía server, không tin `companyId` do trình duyệt gửi lên.
- Woo key/secret được mã hóa bằng ASP.NET Data Protection trước khi lưu.
- Webhook WooCommerce phải xác minh HMAC SHA-256 trên raw request body.
- Có audit log cho thay đổi giá, tồn kho, hóa đơn, cấu hình và phân quyền.

## Ranh giới module

| Module | Trách nhiệm |
| --- | --- |
| Identity | Nhân viên, vai trò, permission, đăng nhập, khóa tài khoản |
| Catalog | Sản phẩm, biến thể, danh mục, thương hiệu, giá và liên kết Woo |
| Inventory | Kho, serial/IMEI, nhập/xuất/điều chỉnh tồn |
| Sales | Hóa đơn bán, thanh toán, trả hàng |
| Purchasing | Nhà cung cấp, hóa đơn nhập, giá vốn |
| CRM | Khách hàng, lịch sử mua, chăm sóc |
| Integrations | WooCommerce, webhook, retry, đối soát |
| Settings | Công ty, chi nhánh, số chứng từ, bí mật tích hợp |
| Audit | Nhật ký thay đổi và sự kiện bảo mật |

## Đồng bộ WooCommerce

Đồng bộ là hai chiều nhưng có **quyền sở hữu theo trường**:

- MTL V2 sở hữu SKU, tồn kho, giá vốn, serial, trạng thái nội bộ.
- Tên, mô tả, ảnh, giá bán và trạng thái website có thể cập nhật từ MTL V2 lên Woo.
- Webhook Woo cập nhật dữ liệu website về MTL V2.
- Khi cả hai bên thay đổi sau lần sync gần nhất, bản ghi chuyển sang `Conflict` để người có quyền xử lý.

Không dùng timestamp duy nhất làm khóa chống lặp. Mỗi webhook/sync job có external ID và unique constraint để bảo đảm idempotency.

## Triển khai

Giai đoạn đầu dùng Docker Compose: `web`, `api`, `postgres`. HTTPS và backup đặt tại reverse proxy/hạ tầng máy chủ. Khi cần scale, tách PostgreSQL sang dịch vụ managed, thêm Redis và worker riêng mà không đổi domain model.

