# MTL V2

Hệ thống quản trị mới cho MỸ THO LAPTOP, được xây dựng độc lập với `APP.MTL` và đồng bộ sản phẩm hai chiều với WooCommerce tại `mytholaptop.vn`.

## Công nghệ

- ASP.NET Core 10 LTS + EF Core 10 cho API và nghiệp vụ.
- React + TypeScript + Vite cho giao diện quản trị.
- PostgreSQL 18 cho dữ liệu hệ thống.
- ASP.NET Core Identity, cookie HttpOnly và permission-based authorization.
- Docker Compose cho môi trường phát triển và triển khai một máy chủ.

## Cấu trúc

```text
src/
  MTL.Domain/          Mô hình và quy tắc nghiệp vụ
  MTL.Infrastructure/  EF Core, Identity, WooCommerce, bảo mật
  MTL.Api/             HTTP API, auth, controllers
  MTL.Web/             React admin portal
tests/
docs/
deploy/
```

## Chạy bằng Docker

1. Sao chép `.env.example` thành `.env` và thay toàn bộ mật khẩu mẫu.
2. Chạy `docker compose up --build`.
3. Mở `http://localhost:8080`.

Tài khoản quản trị đầu tiên được tạo từ `MTL_ADMIN_EMAIL` và `MTL_ADMIN_PASSWORD`. Không commit file `.env`.

## Nguyên tắc dữ liệu WooCommerce

- PostgreSQL là nguồn dữ liệu nghiệp vụ nội bộ: giá vốn, tồn kho, serial, hóa đơn và lịch sử.
- WooCommerce là kênh bán hàng/hiển thị website.
- Mỗi sản phẩm có liên kết Woo ID, phiên bản cập nhật và trạng thái đồng bộ.
- Webhook cập nhật nhanh; tác vụ đối soát định kỳ sửa các lần webhook bị mất.
- Ghi đồng bộ có idempotency, retry và log lỗi; xung đột không được ghi đè âm thầm.

Xem [kiến trúc](docs/ARCHITECTURE.md), [mô hình dữ liệu](docs/DATA_MODEL.md), [triển khai](docs/DEPLOYMENT.md), [migrate từ hệ thống cũ](docs/MIGRATION_FROM_V1.md) và [lộ trình](docs/ROADMAP.md).


