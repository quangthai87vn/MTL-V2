# Mô hình dữ liệu cốt lõi

## Identity và công ty

- `Companies`: thông tin doanh nghiệp/chi nhánh.
- `AspNetUsers`, `AspNetRoles`: tài khoản và vai trò Identity.
- `RolePermissions`: permission gắn với vai trò.
- `AuditLogs`: ai đã làm gì, khi nào, trên thực thể nào.

## Sản phẩm và đồng bộ

- `Products`: SKU, tên, giá, giá vốn, trạng thái và dữ liệu website.
- `ProductVariants`: biến thể theo RAM/SSD/màu hoặc thuộc tính Woo.
- `WooCommerceConnections`: URL và credential đã mã hóa.
- `WooProductLinks`: liên kết local product với Woo product/variation ID.
- `SyncJobs`, `SyncEvents`: trạng thái, retry và lỗi đồng bộ.

## Bán hàng và nhập hàng

- `SalesInvoices`, `SalesInvoiceLines`, `Payments`.
- `PurchaseInvoices`, `PurchaseInvoiceLines`, `Suppliers`.
- Dòng hóa đơn lưu snapshot tên/SKU/đơn giá; không phụ thuộc tên sản phẩm thay đổi sau này.
- Hóa đơn đã phát hành không xóa vật lý; điều chỉnh bằng trạng thái hoặc chứng từ đảo.

## Kho và serial

- `Warehouses`, `StockMovements`, `ProductSerials`.
- Tồn kho được suy ra từ stock ledger, không chỉ cập nhật một con số không có lịch sử.
- Laptop nên quản lý serial, tình trạng, thời hạn bảo hành và giá vốn theo từng máy.

## CRM

- `Customers`, địa chỉ và thông tin liên hệ.
- Lịch sử mua lấy từ hóa đơn; ghi chú/chăm sóc lưu riêng.
- Số điện thoại chuẩn hóa và có unique index trong phạm vi công ty.

