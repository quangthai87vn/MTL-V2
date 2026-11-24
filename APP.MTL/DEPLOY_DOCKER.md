# Hướng Dẫn Triển Khai Docker Registry

## 1. Chuẩn Bị

### 1.1. Đăng ký Docker Hub (miễn phí)

1. Truy cập https://hub.docker.com
2. Đăng ký tài khoản mới (nếu chưa có)
3. Tạo Repository: `mytholaptop_app`

### 1.2. Cài đặt Docker CLI (nếu chưa có)

```powershell
# Kiểm tra đã cài chưa
docker --version
docker login --help
```

---

## 2. Đóng Gói Image

### 2.1. Build Image

```bash
# Tại thư mục dự án APP.MTL
docker build -t mytholaptop_app:latest -f Dockerfile .
```

### 2.2. Tag Image cho Registry

```bash
# Thay YOUR_DOCKERHUB_USERNAME bằng username Docker Hub của bạn
docker tag mytholaptop_app:latest quangthai87/mytholaptop_app:latest

# Nếu muốn đánh version cụ thể
docker tag mytholaptop_app:latest quangthai87/mytholaptop_app:v1.0.0
```

### 2.3. Kiểm tra Image

```bash
docker images | findstr mytholaptop
```

Output mẫu:
```
REPOSITORY                              TAG       IMAGE ID       SIZE
mytholaptop_app                         latest    abc123def456   516MB
YOUR_DOCKERHUB_USERNAME/mytholaptop_app latest    abc123def456   516MB
```

---

## 3. Đẩy lên Docker Hub

### 3.1. Login Docker Hub

```bash
docker login
```

Nhập:
- Username: `YOUR_DOCKERHUB_USERNAME`
- Password: `YOUR_DOCKERHUB_PASSWORD`

### 3.2. Push Image

```bash
# Push version latest
docker push quangthai87/mytholaptop_app:latest

# Push version cụ thể
docker push quangthai87/mytholaptop_app:v1.0.0
```

### 3.3. Xác nhận đã push thành công

Truy cập https://hub.docker.com/repository/docker/quangthai87/mytholaptop_app

---

## 4. Triển Khai trên Server

### 4.1. SSH vào Server

```bash
ssh root@YOUR_SERVER_IP
```

### 4.2. Cài đặt Docker (nếu chưa có)

```bash
# Ubuntu/Debian
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
sudo systemctl enable docker
sudo systemctl start docker
```

### 4.3. Pull Image về Server

```bash
# Login Docker Hub (nếu repository là private)
docker login

# Pull image
docker pull YOUR_DOCKERHUB_USERNAME/mytholaptop_app:latest

# Hoặc pull version cụ thể
docker pull YOUR_DOCKERHUB_USERNAME/mytholaptop_app:v1.0.0
```

### 4.4. Tạo file docker-compose.yml trên Server

```bash
nano docker-compose.yml
```

Nội dung:

```yaml
services:
  mytholaptop_app:
    image: YOUR_DOCKERHUB_USERNAME/mytholaptop_app:latest
    container_name: mytholaptop_app
    restart: unless-stopped
    ports:
      - "5000:8080"
    environment:
      # THAY ĐỔI connection string tại đây
      - ConnectionStrings__MTLDB=Data Source=YOUR_DB_SERVER;Initial Catalog=MTLSale;user=mytholaptop_user;Trust Server Certificate=true;Password=YOUR_PASSWORD
      - ASPNETCORE_ENVIRONMENT=Production
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s

networks:
  default:
    name: mtl_network
```

### 4.5. Khởi chạy

```bash
# Tạo network (nếu chưa có)
docker network create mtl_network 2>/dev/null || true

# Chạy container
docker-compose up -d

# Kiểm tra trạng thái
docker ps | findstr mytholaptop
docker-compose logs -f
```

### 4.6. Kiểm tra ứng dụng

Mở trình duyệt: `http://YOUR_SERVER_IP:5000`

---

## 5. Các Lệnh Quản Lý

```bash
# Xem logs
docker-compose logs -f mytholaptop_app

# Restart
docker-compose restart mytholaptop_app

# Stop
docker-compose down

# Update lên version mới
docker pull YOUR_DOCKERHUB_USERNAME/mytholaptop_app:latest
docker-compose up -d

# Dọn image cũ
docker image prune -f
```

---

## 6. Bảo Mật

### 6.1. Ẩn Connection String

**KHÔNG hardcode** password trong docker-compose.yml.

```bash
# Tạo file .env
nano .env
```

```
MTLDB_CONNECTION=Data Source=YOUR_DB_SERVER;Initial Catalog=MTLSale;user=mytholaptop_user;Trust Server Certificate=true;Password=YOUR_PASSWORD
```

Sau đó trong docker-compose.yml:

```yaml
environment:
  - ConnectionStrings__MTLDB=${MTLDB_CONNECTION}
```

### 6.2. Firewall

```bash
# Mở port 5000
ufw allow 5000/tcp

# Reload firewall
ufw reload
```

---

## 7. HTTPS với Nginx Proxy (Tùy chọn)

```bash
# Cài đặt Nginx + Certbot
sudo apt install nginx certbot python3-certbot-nginx

# Cấu hình Nginx reverse proxy
sudo nano /etc/nginx/sites-available/mytholaptop
```

```nginx
server {
    listen 80;
    server_name yourdomain.com;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

```bash
# Kích hoạt và reload
sudo ln -s /etc/nginx/sites-available/mytholaptop /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx

# SSL miễn phí
sudo certbot --nginx -d yourdomain.com
```

---

## 8. Troubleshooting

```bash
# Container không chạy được
docker-compose logs

# Kiểm tra port bị chiếm
netstat -tlnp | findstr 5000

# Xem container đang chạy gì
docker exec -it mytholaptop_app sh
ls /app
dotnet --list-runtimes
```

---

## Tóm Tắt Nhanh

```bash
# 1. Build & Tag
docker build -t mytholaptop_app:latest -f Dockerfile .
docker tag mytholaptop_app:latest YOUR_USER/mytholaptop_app:latest

# 2. Push
docker login
docker push quangthai87/mytholaptop_app:latest

# 3. Trên Server
docker pull quangthai87/mytholaptop_app:latest
docker-compose up -d
```
