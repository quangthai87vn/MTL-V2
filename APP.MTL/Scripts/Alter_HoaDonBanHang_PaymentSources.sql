-- Bổ sung cột phân tách nguồn tiền trên hóa đơn bán hàng (tiền mặt / CK đã nhận / CK dự kiến).
-- Chạy trên SQL Server trước khi publish bản app có đọc các cột này.

IF COL_LENGTH('dbo.tbl_HoaDonBanHang', 'tienmat') IS NULL
    ALTER TABLE dbo.tbl_HoaDonBanHang ADD tienmat FLOAT NULL;
GO
IF COL_LENGTH('dbo.tbl_HoaDonBanHang', 'tienchuyenkhoan') IS NULL
    ALTER TABLE dbo.tbl_HoaDonBanHang ADD tienchuyenkhoan FLOAT NULL;
GO
IF COL_LENGTH('dbo.tbl_HoaDonBanHang', 'tienchuyenkhoandukien') IS NULL
    ALTER TABLE dbo.tbl_HoaDonBanHang ADD tienchuyenkhoandukien FLOAT NULL;
GO
IF COL_LENGTH('dbo.tbl_HoaDonBanHang', 'ngaydukienchuyenkhoan') IS NULL
    ALTER TABLE dbo.tbl_HoaDonBanHang ADD ngaydukienchuyenkhoan DATETIME NULL;
GO

-- Gán lại dữ liệu cũ (ước lượng từ hình thức thanh toán hiện có)
UPDATE dbo.tbl_HoaDonBanHang
SET tienmat = CASE WHEN idhinhthucthanhtoan = 1 THEN ISNULL(thanhtoan, 0) ELSE 0 END,
    tienchuyenkhoan = CASE WHEN idhinhthucthanhtoan IN (2, 4) THEN ISNULL(thanhtoan, 0) ELSE 0 END,
    tienchuyenkhoandukien = CASE WHEN idhinhthucthanhtoan = 3 THEN ISNULL(conlai, 0) ELSE 0 END
WHERE (tienmat IS NULL OR tienmat = 0)
  AND (tienchuyenkhoan IS NULL OR tienchuyenkhoan = 0)
  AND (tienchuyenkhoandukien IS NULL OR tienchuyenkhoandukien = 0);

-- Trả góp: phần đã thu thường là tiền mặt
UPDATE dbo.tbl_HoaDonBanHang
SET tienmat = ISNULL(thanhtoan, 0)
WHERE idhinhthucthanhtoan = 3
  AND (tienmat IS NULL OR tienmat = 0);
