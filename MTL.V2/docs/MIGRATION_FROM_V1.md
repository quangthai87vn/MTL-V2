# Chuyển dữ liệu hóa đơn và khách hàng từ APP.MTL

Công cụ `tools/MTL.LegacyMigration` chỉ đọc SQL Server của APP.MTL và ghi dữ liệu đã chuẩn hóa vào PostgreSQL của MTL.V2. Chế độ mặc định là `dry-run`; chỉ ghi thật khi có cờ `--apply`.

## Phạm vi đã khảo sát

Nguồn hiện hành:

- Khách hàng: `tbl_users`, nhóm khách hàng của công ty MỸ THO LAPTOP.
- Hóa đơn/phiếu: `tbl_HoaDonBanHang`.
- Chi tiết hóa đơn: `tbl_HoaDonBanHangChiTiet`.
- Thẻ dịch vụ: `Tag` và `tbl_HoaDonBanHangTagMap`.

Nguồn lưu trữ cũ:

- Hóa đơn: `tblHoaDon`.
- Chi tiết hóa đơn: `tblHoaDonChiTiet`.

Báo giá được thống kê trong báo cáo dry-run nhưng không nhập vào doanh thu, nguồn tiền hay danh sách hóa đơn. Hóa đơn bán hàng, dịch vụ và đặt hàng được nhập; kho hóa đơn cũ cũng được nhập để giữ lịch sử tra cứu.

## Ánh xạ dữ liệu

| APP.MTL | MTL.V2 | Quy tắc |
| --- | --- | --- |
| `tbl_users.id` | `Customers.LegacyId` | Dạng `customer:{id}`; dùng để chạy lại không nhân đôi |
| `tbl_users.makh` | `Customers.CitizenId` | CCCD; giữ nguyên chuỗi sau khi bỏ khoảng trắng đầu/cuối |
| Họ tên, điện thoại, email, địa chỉ, MST | Hồ sơ khách hàng tương ứng | Chuẩn hóa số điện thoại về chữ số |
| Mã phiếu hiện hành | `SalesInvoices.Number` | Giữ nguyên mã nguồn |
| Hóa đơn lưu trữ | `SalesInvoices.Number` | Dạng `OLD-{id}` để không đụng mã hiện hành |
| `ngaylapphieu` | `InvoiceDateUtc` | Hiểu theo múi giờ Việt Nam rồi chuyển sang UTC |
| Tổng tiền nguồn | `TotalAmount` | Giữ đúng tổng tiền đầu hóa đơn, không tính lại từ tên sản phẩm |
| Dòng hàng/dịch vụ | `SalesInvoiceLines` | Giữ tên, số lượng, đơn giá, thành tiền, bảo hành và ghi chú |
| Đã xử lý | `RepairStatus.Returned` | Được tính doanh thu và nguồn tiền |
| Chưa xử lý | `RepairStatus.InProgress` | Hiển thị “Đang xử lý”, chưa tính doanh thu cho đến khi hoàn tất |
| Thanh toán | `SalesPayments.Cash` | Toàn bộ hóa đơn nhập với nguồn tiền mặt theo yêu cầu; hóa đơn đổi/trả có tổng âm được ghi khoản tiền mặt âm để đối soát |
| Thẻ dịch vụ | `ServiceTypes` | Tái sử dụng theo tên; tự tạo danh mục còn thiếu |

Các hóa đơn không còn dòng chi tiết sẽ có một dòng đối soát theo đúng tổng hóa đơn. Dòng chi tiết mồ côi trong kho lưu trữ được loại khỏi dữ liệu nhập và ghi rõ trong báo cáo.

## Quy tắc ghép khách hàng

1. Ưu tiên khóa nguồn `APP.MTL + customer:{id}`.
2. Nếu chưa có khóa nguồn, chỉ ghép bằng số điện thoại khi số đó duy nhất ở cả nguồn và đích.
3. Số điện thoại trùng được giữ thành các hồ sơ riêng, không tự gộp.
4. Hóa đơn trỏ tới khách hàng đã bị xóa vẫn được giữ bằng thông tin snapshot trên hóa đơn.
5. Hóa đơn lưu trữ không có mã khách hàng chỉ ghép khi họ tên chuẩn hóa là duy nhất.

## An toàn và khả năng chạy lại

- `LegacySource` và `LegacyId` có unique index theo công ty cho khách hàng và hóa đơn.
- Toàn bộ lần ghi chạy trong một transaction; lỗi ở bất kỳ batch nào sẽ rollback.
- Nếu mã hóa đơn nguồn trùng dữ liệu MTL.V2 không thuộc APP.MTL, công cụ dừng trước khi ghi.
- Mỗi lần chạy tạo báo cáo JSON gồm số bản ghi, tổng tiền, cảnh báo, số đã ghép và số đã thêm.
- Có thể chạy lại `--apply`; các bản ghi đã nhập được bỏ qua theo khóa nguồn.
- Cuối quá trình chuyển đổi cần ngừng ghi ở APP.MTL, chạy lại một lần để lấy các hóa đơn phát sinh mới rồi mới cho người dùng làm việc hoàn toàn trên MTL.V2. Công cụ không tự ghi ngược về hệ thống cũ.

## Cách chạy

Biến môi trường bắt buộc:

- `MTL_TARGET_CONNECTION_STRING`: PostgreSQL của MTL.V2.
- `MTL_TARGET_COMPANY_ID`: ID công ty đích.
- Một trong hai nguồn: `MTL_LEGACY_CONNECTION_STRING` hoặc `--source-config <appsettings.json của APP.MTL>`.

Dry-run:

```powershell
dotnet run --project tools/MTL.LegacyMigration -c Release -- `
  --source-config "..\APP.MTL\appsettings.json" `
  --report "artifacts\migration\app-mtl-dry-run.json"
```

Ghi dữ liệu sau khi đối soát báo cáo:

```powershell
dotnet run --project tools/MTL.LegacyMigration -c Release -- `
  --apply `
  --source-config "..\APP.MTL\appsettings.json" `
  --report "artifacts\migration\app-mtl-apply.json"
```

Thêm `--no-archive` nếu chỉ muốn nhập bảng hóa đơn hiện hành. Sau khi ghi, cần đối chiếu ít nhất: tổng số hóa đơn, tổng tiền, số dòng chi tiết, số phiếu đang xử lý, số CCCD và các cảnh báo trong báo cáo.

Hóa đơn tổng bằng 0 không tạo dòng tiền. Hóa đơn tổng âm tạo dòng tiền mặt âm, nhờ đó tổng nguồn tiền của dữ liệu chuyển đổi luôn bằng đúng tổng hóa đơn.

