-- Bản ghi cũ có Ngaygui = 0001-01-01 do ứng dụng không gán Ngaygui khi INSERT
-- (EF Core vẫn gửi giá trị mặc định C# → SQL không dùng DEFAULT SYSDATETIME()).
-- Code mới đã gán Ngaygui + InsertLogAsync tự điền nếu thiếu.

-- TÙY CHỌN — chỉ chạy nếu bạn muốn ghi đè ngày sai bằng «thời điểm chạy script»
-- (KHÔNG phải thời điểm gửi ZNS thật; chỉ để lọc/sắp xếp trong DB).
/*
UPDATE dbo.tbl_ZnsGuiNhatKy
SET Ngaygui = SYSDATETIME()
WHERE Ngaygui < '1900-01-01';
*/
