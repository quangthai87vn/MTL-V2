-- =============================================================================
-- Tạm thời gộp toàn bộ số đã thu (TM + CK + CK dự kiến) vào TIỀN MẶT cho các
-- phiếu có ngày trả phiếu TRƯỚC 01/04/2026 — khớp logic báo cáo nguồn tiền
-- (ReportService: Tienmat, Tienchuyenkhoan, Tienchuyenkhoandukien, Idbank).
--
-- CẢNH BÁO:
--   - Sao lưu DB trước khi chạy.
--   - Chạy khối SELECT xem trước, rồi mới UPDATE.
--   - Chỉ dùng khi chấp nhận mất phân tách TK ngân hàng / trả góp trên dữ liệu cũ.
--   - Không sửa thanhtoan/conlai ở đây (trả góp còn nợ có thể khác); nếu màn hình
--     sửa phiếu lệch, cần xử lý riêng.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @NgayCat DATETIME = '20260401'; -- chỉ các phiếu có ngaytraphieu < ngày này
DECLARE @Idcompany INT = NULL;        -- NULL = mọi công ty; hoặc gán ví dụ 1

/* --- Xem trước: số dòng & tổng tiền sẽ gộp --- */
SELECT COUNT(*) AS SoPhieu,
       SUM(ISNULL(tienmat, 0) + ISNULL(tienchuyenkhoan, 0) + ISNULL(tienchuyenkhoandukien, 0)) AS TongTienSauKhiGop
FROM dbo.tbl_HoaDonBanHang
WHERE ngaytraphieu IS NOT NULL
  AND ngaytraphieu < @NgayCat
  AND (@Idcompany IS NULL OR idcompany = @Idcompany);

/* Chi tiết (giới hạn 200 dòng) — bỏ comment để xem */
/*
SELECT TOP 200
    mahd,
    ngaytraphieu,
    idcompany,
    tienmat,
    tienchuyenkhoan,
    tienchuyenkhoandukien,
    idbank,
    idhinhthucthanhtoan
FROM dbo.tbl_HoaDonBanHang
WHERE ngaytraphieu IS NOT NULL
  AND ngaytraphieu < @NgayCat
  AND (@Idcompany IS NULL OR idcompany = @Idcompany)
ORDER BY ngaytraphieu;
*/

BEGIN TRANSACTION;

UPDATE dbo.tbl_HoaDonBanHang
SET tienmat = ISNULL(tienmat, 0) + ISNULL(tienchuyenkhoan, 0) + ISNULL(tienchuyenkhoandukien, 0),
    tienchuyenkhoan = 0,
    tienchuyenkhoandukien = 0,
    idbank = NULL,
    ngaydukienchuyenkhoan = NULL
    -- Tuỳ chọn: ép hình thức hiển thị = Tiền mặt (1) — có thể làm lệch màn hình sửa phiếu trả góp
    -- , idhinhthucthanhtoan = 1
    -- , lastupdate = GETDATE()
WHERE ngaytraphieu IS NOT NULL
  AND ngaytraphieu < @NgayCat
  AND (@Idcompany IS NULL OR idcompany = @Idcompany);

-- COMMIT TRANSACTION;
ROLLBACK TRANSACTION; -- Đổi thành COMMIT sau khi đã kiểm tra kỹ trên bản sao / staging.

PRINT N'Hiện đang ROLLBACK để an toàn. Sửa script: bỏ ROLLBACK, dùng COMMIT khi chạy thật.';
