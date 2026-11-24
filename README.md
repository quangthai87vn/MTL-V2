# MTL Workspace

Monorepo lưu toàn bộ mã nguồn của MỸ THO LAPTOP, gồm hệ thống cũ và hệ thống đang phát triển.

## Cấu trúc

- `APP.MTL/`: hệ thống cũ, dùng để đối chiếu nghiệp vụ và dữ liệu lịch sử.
- `MTL.V2/`: hệ thống mới đang được phát triển và vận hành.

## Quy trình phát triển

Mọi lệnh Git được chạy từ thư mục gốc `E:\PROJECT MTL` để một commit có thể ghi nhận đầy đủ thay đổi liên quan giữa hai hệ thống.

```powershell
git checkout -b feat/ten-tinh-nang
git status
git add APP.MTL MTL.V2
git commit -m "feat: mô tả tính năng"
git push -u origin feat/ten-tinh-nang
```

Sau khi tính năng ổn định, tạo Pull Request về nhánh `main`. GitHub Actions sẽ tự động build và test mã nguồn trước khi hợp nhất.

## Kiểm tra hệ thống mới

```powershell
dotnet restore .\MTL.V2\MTL.V2.slnx
dotnet build .\MTL.V2\MTL.V2.slnx -c Release --no-restore
dotnet test .\MTL.V2\MTL.V2.slnx -c Release --no-build

Set-Location .\MTL.V2\src\MTL.Web
npm ci
npm run build
```

## Kiểm tra hệ thống cũ

```powershell
dotnet restore .\APP.MTL\MTL.APP.csproj
dotnet build .\APP.MTL\MTL.APP.csproj -c Release --no-restore
```

## Cấu hình cục bộ

Các file `.env`, `appsettings*.json`, dữ liệu runtime, bản dump cơ sở dữ liệu và thư mục build không được đưa lên GitHub. Dùng file `.env.example` làm mẫu và giữ mật khẩu/khóa truy cập ở máy triển khai.

Repository: <https://github.com/quangthai87vn/MTL-V2>

