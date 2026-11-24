-- Nhật ký gửi ZNS (chiến dịch / đơn lẻ). Chạy một lần trên SQL Server.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'tbl_ZnsGuiNhatKy')
BEGIN
    CREATE TABLE dbo.tbl_ZnsGuiNhatKy (
        Id              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Idcompany       INT NULL,
        Iduser          INT NULL,
        Mahd            NVARCHAR(30) NULL,
        Idkhachhang     INT NULL,
        Sodienthoai     NVARCHAR(50) NULL,
        Tenkhachhang    NVARCHAR(200) NULL,
        Idtemplate      INT NULL,
        Tenmau          NVARCHAR(500) NULL,
        Loaidichvu      NVARCHAR(100) NULL,
        Noidungxemtruoc NVARCHAR(MAX) NULL,
        RequestUrl      NVARCHAR(2000) NULL,
        HttpStatus      INT NULL,
        ResponseBody    NVARCHAR(MAX) NULL,
        Thanhcong       BIT NOT NULL CONSTRAINT DF_tbl_ZnsGuiNhatKy_Thanhcong DEFAULT (0),
        Ghichu          NVARCHAR(1000) NULL,
        Ngaygui         DATETIME2 NOT NULL CONSTRAINT DF_tbl_ZnsGuiNhatKy_Ngaygui DEFAULT (SYSDATETIME())
    );
    CREATE INDEX IX_tbl_ZnsGuiNhatKy_Mahd ON dbo.tbl_ZnsGuiNhatKy (Mahd);
    CREATE INDEX IX_tbl_ZnsGuiNhatKy_Ngaygui ON dbo.tbl_ZnsGuiNhatKy (Ngaygui DESC);
    CREATE INDEX IX_tbl_ZnsGuiNhatKy_Idcompany ON dbo.tbl_ZnsGuiNhatKy (Idcompany);
END
GO
