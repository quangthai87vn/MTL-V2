-- =====================================================
-- Script tạo bảng Facebook_Messages
-- Chạy script này trên SQL Server để tạo bảng lưu trữ tin nhắn Facebook
-- =====================================================

-- Tạo bảng Facebook_Messages
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Facebook_Messages')
BEGIN
    CREATE TABLE [dbo].[Facebook_Messages] (
        [id] [INT] IDENTITY(1,1) NOT NULL,
        [conversation_id] [NVARCHAR](100) NULL,
        [page_id] [NVARCHAR](100) NULL,
        [sender_name] [NVARCHAR](250) NULL,
        [sender_avatar] [NVARCHAR](500) NULL,
        [message] [NVARCHAR](MAX) NULL,
        [message_type] [NVARCHAR](50) NULL,
        [media_url] [NVARCHAR](500) NULL,
        [facebook_message_id] [NVARCHAR](100) NULL,
        [is_from_customer] [BIT] NOT NULL DEFAULT 1,
        [timestamp] [DATETIME] NULL DEFAULT GETDATE(),
        [is_read] [BIT] NOT NULL DEFAULT 0,
        [is_processed] [BIT] NOT NULL DEFAULT 0,
        [reply_by_user_id] [INT] NULL,
        [reply_timestamp] [DATETIME] NULL,
        [internal_note] [NVARCHAR](MAX) NULL,
        [idcompany] [INT] NOT NULL DEFAULT 1,
        CONSTRAINT [PK_FacebookMessages] PRIMARY KEY CLUSTERED ([id] ASC)
    );

    -- Tạo các index để tăng tốc truy vấn
    CREATE NONCLUSTERED INDEX [IX_FacebookMessages_ConversationId] ON [dbo].[Facebook_Messages]([conversation_id]);
    CREATE NONCLUSTERED INDEX [IX_FacebookMessages_PageId] ON [dbo].[Facebook_Messages]([page_id]);
    CREATE NONCLUSTERED INDEX [IX_FacebookMessages_Timestamp] ON [dbo].[Facebook_Messages]([timestamp]);
    CREATE NONCLUSTERED INDEX [IX_FacebookMessages_FacebookMessageId] ON [dbo].[Facebook_Messages]([facebook_message_id]);

    PRINT 'Bảng Facebook_Messages đã được tạo thành công!';
END
ELSE
BEGIN
    PRINT 'Bảng Facebook_Messages đã tồn tại.';
END
GO
