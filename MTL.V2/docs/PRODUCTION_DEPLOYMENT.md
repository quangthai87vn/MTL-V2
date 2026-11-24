# Triển khai MTL.V2 trên Debian 12

## Mô hình dữ liệu

PostgreSQL chạy trong container nhưng dữ liệu nằm trong Docker named volume `postgres-data`. Image API/Web có thể thay đổi hoặc bị xóa mà không làm mất dữ liệu trong volume.

Không mở cổng PostgreSQL ra Internet. Chỉ dịch vụ API trong cùng Docker network được truy cập database.

## Chuẩn bị máy chủ

Cho phép user triển khai chạy Docker mà không cần `sudo`:

```bash
sudo usermod -aG docker "$USER"
newgrp docker
docker info >/dev/null && echo "Docker OK"
```

Nhóm `docker` có quyền quản trị máy chủ. Chỉ thêm tài khoản dùng để triển khai, không thêm tài khoản người dùng thông thường.

```bash
sudo mkdir -p /opt/mtl-v2
sudo chown -R "$USER":"$USER" /opt/mtl-v2
git clone git@github.com:quangthai87vn/MTL-V2.git /opt/mtl-v2/source
cd /opt/mtl-v2/source/MTL.V2
cp deploy/production.env.example .env
chmod 600 .env
```

Điền mật khẩu thật vào `.env`. Sau đó tạo **Personal access token (classic)** có quyền `read:packages` và đăng nhập GitHub Container Registry. Không dùng mật khẩu tài khoản GitHub:

```bash
read -rsp "GitHub package token: " GHCR_TOKEN; echo
printf '%s' "$GHCR_TOKEN" | docker login ghcr.io -u quangthai87vn --password-stdin
unset GHCR_TOKEN
```

Thông tin đăng nhập Docker được lưu riêng theo từng user. Vì vậy không đăng nhập bằng `docker login` rồi chạy `sudo ./scripts/deploy-prod.sh`; khi đó lệnh chạy dưới user `root` và không đọc được thông tin đăng nhập của user hiện tại.

Kiểm tra quyền truy cập image trước khi triển khai:

```bash
docker pull ghcr.io/quangthai87vn/mtl-v2-api:main
docker pull ghcr.io/quangthai87vn/mtl-v2-web:main
```

## Triển khai

```bash
cd /opt/mtl-v2/source
git pull --ff-only origin main
cd MTL.V2
chmod +x scripts/*.sh
./scripts/deploy-prod.sh main
```

### Sửa lỗi `unauthorized` khi pull image

Nếu trước đó đã chạy script bằng `sudo`, thực hiện lại bằng đúng user triển khai:

```bash
sudo usermod -aG docker "$USER"
newgrp docker

docker logout ghcr.io || true
read -rsp "GitHub package token: " GHCR_TOKEN; echo
printf '%s' "$GHCR_TOKEN" | docker login ghcr.io -u quangthai87vn --password-stdin
unset GHCR_TOKEN

docker pull ghcr.io/quangthai87vn/mtl-v2-api:main
docker pull ghcr.io/quangthai87vn/mtl-v2-web:main
./scripts/deploy-prod.sh main
```

Nếu vẫn báo `unauthorized`, kiểm tra token là loại classic, có quyền `read:packages`, chưa hết hạn và thuộc tài khoản có quyền truy cập repository/package.

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
