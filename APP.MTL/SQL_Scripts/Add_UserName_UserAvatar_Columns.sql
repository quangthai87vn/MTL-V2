-- Script thêm columns user_name và user_avatar vào bảng ZaloOA_Messages
-- Chạy script này trong SQL Server

-- Thêm cột user_name (nvarchar để hỗ trợ tiếng Việt)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('ZaloOA_Messages')
    AND name = 'user_name'
)
BEGIN
    ALTER TABLE ZaloOA_Messages
    ADD [user_name] NVARCHAR(500) NULL;
    PRINT 'Added column: user_name';
END
ELSE
BEGIN
    PRINT 'Column user_name already exists';
END

-- Thêm cột user_avatar
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('ZaloOA_Messages')
    AND name = 'user_avatar'
)
BEGIN
    ALTER TABLE ZaloOA_Messages
    ADD [user_avatar] NVARCHAR(1000) NULL;
    PRINT 'Added column: user_avatar';
END
ELSE
BEGIN
    PRINT 'Column user_avatar already exists';
END

-- Cập nhật dữ liệu cũ (nếu có user_id) bằng cách gọi Zalo API
-- Hoặc set default:
UPDATE ZaloOA_Messages
SET [user_name] = 'Khach hang'
WHERE [user_name] IS NULL AND [user_id] IS NOT NULL;

PRINT 'Update completed';
