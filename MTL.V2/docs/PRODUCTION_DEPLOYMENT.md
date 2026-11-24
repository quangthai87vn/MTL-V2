# Triển khai MTL.V2 trên Debian 12

## Mô hình dữ liệu

PostgreSQL chạy trong container nhưng dữ liệu nằm trong Docker named volume `postgres-data`. Image API/Web có thể thay đổi hoặc bị xóa mà không làm mất dữ liệu trong volume.

Không mở cổng PostgreSQL ra Internet. Chỉ dịch vụ API trong cùng Docker network được truy cập database.

## Chuẩn bị máy chủ

```bash
sudo mkdir -p /opt/mtl-v2
sudo chown -R "$USER":"$USER" /opt/mtl-v2
git clone git@github.com:quangthai87vn/MTL-V2.git /opt/mtl-v2/source
cd /opt/mtl-v2/source/MTL.V2
cp deploy/production.env.example .env
chmod 600 .env
```

Điền mật khẩu thật vào `.env`, sau đó đăng nhập GitHub Container Registry bằng token chỉ có quyền `read:packages`:

```bash
docker login ghcr.io -u quangthai87vn
```

## Triển khai

```bash
cd /opt/mtl-v2/source
git pull --ff-only origin main
cd MTL.V2
chmod +x scripts/*.sh
./scripts/deploy-prod.sh main
```

Để triển khai đúng một commit đã được build:

```bash
./scripts/deploy-prod.sh sha-COMMIT_SHA
```

## Sao lưu và khôi phục PostgreSQL

Tạo backup thủ công:

```bash
./scripts/backup-postgres.sh
```

Backup được lưu mặc định tại `backups/postgres` và giữ 14 ngày. Nên sao chép backup sang ổ đĩa hoặc máy chủ khác.

Khôi phục một file backup:

```bash
cat backups/postgres/mtl-v2-YYYYMMDD-HHMMSS.dump | \
  docker compose --env-file .env -f compose.prod.yml exec -T postgres \
  sh -c 'pg_restore --clean --if-exists --no-owner --no-privileges --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"'
```

## Reverse proxy

Web chỉ lắng nghe tại `127.0.0.1:8080`. Dùng Caddy hoặc Nginx trên máy chủ để cung cấp HTTPS và reverse proxy tới địa chỉ này.

## Lưu ý migration

API tự chạy EF Core migration khi khởi động. Script triển khai tạo backup trước khi thay image. Rollback image không tự rollback schema database; migration có thao tác xóa dữ liệu phải có kế hoạch phục hồi riêng.
