-- Script tạo bảng ZaloOA_Messages trong SQL Server
-- Chạy script này trong SQL Server Management Studio hoặc Azure Data Studio

-- Tạo bảng ZaloOA_Messages nếu chưa tồn tại
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ZaloOA_Messages')
BEGIN
    CREATE TABLE ZaloOA_Messages (
        id INT IDENTITY(1,1) PRIMARY KEY,
        idcompany INT NOT NULL DEFAULT 1,
        user_id NVARCHAR(100) NOT NULL,
        zalo_message_id NVARCHAR(100) NULL,
        message NVARCHAR(MAX) NOT NULL,
        message_type NVARCHAR(50) NOT NULL DEFAULT 'text',
        media_url NVARCHAR(500) NULL,
        file_name NVARCHAR(255) NULL,
        file_size BIGINT NULL,
        link_url NVARCHAR(500) NULL,
        link_title NVARCHAR(255) NULL,
        link_description NVARCHAR(500) NULL,
        link_thumb_url NVARCHAR(500) NULL,
        sticker_id NVARCHAR(100) NULL,
        sticker_url NVARCHAR(500) NULL,
        latitude FLOAT NULL,
        longitude FLOAT NULL,
        address NVARCHAR(500) NULL,
        is_from_customer BIT NOT NULL DEFAULT 1,
        is_read BIT NOT NULL DEFAULT 0,
        timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        updated_at DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    -- Tạo index để tăng tốc truy vấn
    CREATE NONCLUSTERED INDEX IX_ZaloOA_Messages_UserId 
        ON ZaloOA_Messages(user_id, idcompany);
    
    CREATE NONCLUSTERED INDEX IX_ZaloOA_Messages_Timestamp 
        ON ZaloOA_Messages(timestamp DESC);

    PRINT 'Bảng ZaloOA_Messages đã được tạo thành công!';
END
ELSE
BEGIN
    PRINT 'Bảng ZaloOA_Messages đã tồn tại.';
END
