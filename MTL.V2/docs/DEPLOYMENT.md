# Triển khai MTL V2

## Môi trường đề xuất

### Giai đoạn đầu: một máy chủ

- VPS Linux 4 vCPU, 8 GB RAM, SSD từ 100 GB.
- Docker Engine + Docker Compose.
- PostgreSQL chạy container riêng hoặc dịch vụ managed.
- Caddy/Nginx ở phía trước để cấp HTTPS và reverse proxy.
- Backup PostgreSQL hằng ngày sang một máy chủ/object storage khác.

Chạy toàn bộ bằng một máy chủ phù hợp khi số người dùng đồng thời còn thấp và giúp vận hành đơn giản. Không mở cổng PostgreSQL ra Internet ở production.

### Khi hệ thống lớn hơn

- Chuyển PostgreSQL sang dịch vụ managed có point-in-time recovery.
- Chạy API và web trên hai instance sau load balancer.
- Tách Woo sync worker, thêm Redis khi cần hàng đợi phân tán.
- Đưa ảnh/tài liệu sang S3-compatible object storage.

## Quy trình release

1. CI chạy build backend, unit test, lint và build frontend.
2. Tạo image có tag bằng Git commit SHA, không dùng tag `latest` cho production.
3. Backup database trước migration.
4. Chạy migration bằng một release job duy nhất.
5. Deploy API và web, kiểm tra `/api/health`.
6. Chạy `scripts/smoke-test.ps1` bằng tài khoản kiểm thử không phải admin chính.
7. Nếu lỗi, rollback image; migration cần có kế hoạch rollback dữ liệu riêng.

## Bí mật

- `.env` chỉ dùng local; production dùng Docker secrets, Vault hoặc secret manager của cloud.
- Không ghi Woo Secret, database password hoặc webhook secret vào Git/CI log.
- Thư mục Data Protection keys phải được mount bền vững và backup; mất key sẽ làm credential Woo đã mã hóa không thể giải mã.
- Bật HTTPS trước khi chuyển `ASPNETCORE_ENVIRONMENT=Production` vì cookie production có cờ `Secure`.

## Backup tối thiểu

- `pg_dump` mỗi đêm, giữ 7 bản ngày + 4 bản tuần + 6 bản tháng.
- Mỗi tháng thực hiện restore thử vào database độc lập.
- Backup Data Protection keys cùng lịch nhưng lưu tách quyền truy cập khỏi database dump.
- Theo dõi dung lượng, lỗi backup và thời gian khôi phục thực tế.

