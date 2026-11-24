-- =====================================================
-- Script tạo bảng Thu hàng hoá cho MTLSale
-- Chạy trên SQL Server: 123.30.110.181,1433
-- =====================================================

USE MTLSale;
GO

-- =====================================================
-- Bảng chính: TblThuHangHoa
-- =====================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'TblThuHangHoa' AND xtype = 'U')
BEGIN
    CREATE TABLE TblThuHangHoa (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        Maphieu         NVARCHAR(50) NULL,
        Ngaythu         DATETIME NULL,
        Idkhachhang     INT NULL,                    -- FK -> tbl_users (bảng khách hàng)
        Tongtienthu     FLOAT NULL DEFAULT 0,
        Giamgia         FLOAT NULL DEFAULT 0,
        Thanhtoan       FLOAT NULL DEFAULT 0,
        IsThanhToan     BIT NULL DEFAULT 0,
        Idcompany       INT NULL,
        Idnguoithu      INT NULL,
        Ghichu          NVARCHAR(MAX) NULL,
        Beginupdate     DATETIME NULL DEFAULT GETDATE(),
        Lastupdate      DATETIME NULL DEFAULT GETDATE(),
        Iduserlastupdate INT NULL
    );
    PRINT 'Created: TblThuHangHoa';
END
ELSE
    PRINT 'Already exists: TblThuHangHoa';
GO

-- =====================================================
-- ALTER bảng TblThuHangHoa: xóa các cột dư thừa (lưu trong tbl_users)
-- =====================================================
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TblThuHangHoa') AND name = 'Tenkhachhang')
BEGIN
    ALTER TABLE TblThuHangHoa DROP COLUMN Tenkhachhang;
    PRINT 'Dropped column: Tenkhachhang';
END
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TblThuHangHoa') AND name = 'Sdtkhachhang')
BEGIN
    ALTER TABLE TblThuHangHoa DROP COLUMN Sdtkhachhang;
    PRINT 'Dropped column: Sdtkhachhang';
END
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TblThuHangHoa') AND name = 'Diachi')
BEGIN
    ALTER TABLE TblThuHangHoa DROP COLUMN Diachi;
    PRINT 'Dropped column: Diachi';
END
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TblThuHangHoa') AND name = 'Cmndcccd')
BEGIN
    ALTER TABLE TblThuHangHoa DROP COLUMN Cmndcccd;
    PRINT 'Dropped column: Cmndcccd';
END
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TblThuHangHoa') AND name = 'Hinhanhcccd')
BEGIN
    ALTER TABLE TblThuHangHoa DROP COLUMN Hinhanhcccd;
    PRINT 'Dropped column: Hinhanhcccd';
END
GO

-- =====================================================
-- Bảng chi tiết: TblThuHangHoaChiTiet
-- =====================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'TblThuHangHoaChiTiet' AND xtype = 'U')
BEGIN
    CREATE TABLE TblThuHangHoaChiTiet (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        Idphieu         INT NULL,
        Maphieu         NVARCHAR(50) NULL,
        Tensanpham      NVARCHAR(255) NULL,
        Motasanpham     NVARCHAR(MAX) NULL,
        Soluong         FLOAT NULL DEFAULT 1,
        Dongia          FLOAT NULL DEFAULT 0,
        Thanhtien       FLOAT NULL DEFAULT 0,
        Idtinhtrang     INT NULL DEFAULT 1,
        Ghichu          NVARCHAR(MAX) NULL,
        Beginupdate     DATETIME NULL DEFAULT GETDATE(),
        Lastupdate      DATETIME NULL DEFAULT GETDATE(),
        CONSTRAINT FK_ThuHangHoaChiTiet_ThuHangHoa
            FOREIGN KEY (Idphieu) REFERENCES TblThuHangHoa(Id) ON DELETE CASCADE
    );
    PRINT 'Created: TblThuHangHoaChiTiet';
END
ELSE
    PRINT 'Already exists: TblThuHangHoaChiTiet';
GO

-- =====================================================
-- Stored Procedure: uf_GetAllThuHangHoa
-- =====================================================
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'uf_GetAllThuHangHoa' AND type IN ('P', 'FN'))
    DROP PROCEDURE uf_GetAllThuHangHoa;
GO

CREATE PROCEDURE uf_GetAllThuHangHoa
    @idcompany INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        thh.Id                           AS id,
        thh.Maphieu                      AS maphieu,
        thh.Ngaythu                      AS ngaythu,
        thh.Idkhachhang                  AS idkhachhang,
        ISNULL(kh.Fullname, N'Không rõ') AS tenkhachhang,
        kh.Dienthoai                     AS sdtkhachhang,
        kh.Diachi                        AS diachi,
        kh.Makh                          AS cmndcccd,
        thh.Tongtienthu                  AS tongtienthu,
        thh.Giamgia                      AS giamgia,
        thh.Thanhtoan                    AS thanhtoan,
        thh.IsThanhToan                  AS isThanhToan,
        thh.Idcompany                    AS idcompany,
        thh.Idnguoithu                   AS idnguoithu,
        thh.Ghichu                       AS ghichu,
        thh.Beginupdate                  AS beginupdate,
        thh.Lastupdate                   AS lastupdate,
        u.fullname                       AS tennhanvien
    FROM dbo.TblThuHangHoa thh
    -- Người thu: JOIN tbl_users
    LEFT JOIN dbo.tbl_users u ON thh.Idnguoithu = u.id
    -- Khách hàng: JOIN tbl_users qua Idkhachhang
    LEFT JOIN dbo.tbl_users kh ON thh.Idkhachhang = kh.id
    WHERE (@idcompany IS NULL OR thh.Idcompany = @idcompany)
    ORDER BY thh.Ngaythu DESC, thh.Id DESC;
END;
GO

PRINT 'Created: uf_GetAllThuHangHoa';
GO

-- =====================================================
-- Stored Procedure: CreateMaThuHangHoa (tạo mã tự động)
-- =====================================================
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'CreateMaThuHangHoa' AND type IN ('P', 'FN'))
    DROP PROCEDURE CreateMaThuHangHoa;
GO

CREATE PROCEDURE CreateMaThuHangHoa
    @MaTuDong NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Nam NVARCHAR(4) = CAST(YEAR(GETDATE()) AS NVARCHAR(4));
    DECLARE @Thang NVARCHAR(2) = RIGHT('0' + CAST(MONTH(GETDATE()) AS NVARCHAR(2)), 2);
    DECLARE @Ngay NVARCHAR(2) = RIGHT('0' + CAST(DAY(GETDATE()) AS NVARCHAR(2)), 2);
    DECLARE @Prefix NVARCHAR(20) = @MaTuDong + @Nam + @Thang + @Ngay;
    DECLARE @MaxMa NVARCHAR(50);
    SELECT TOP 1 @MaxMa = Maphieu
    FROM dbo.TblThuHangHoa
    WHERE Maphieu LIKE @Prefix + '%'
    ORDER BY Id DESC;
    IF @MaxMa IS NULL
        SELECT @Prefix + '001' AS Ma;
    ELSE
    BEGIN
        DECLARE @Num NVARCHAR(10) = CAST(RIGHT(@MaxMa, 3) AS INT) + 1;
        SELECT @Prefix + RIGHT('000' + CAST(@Num AS NVARCHAR(3)), 3) AS Ma;
    END
END;
GO

PRINT 'Created: CreateMaThuHangHoa';
GO

-- =====================================================
-- Stored Procedure: uf_ThongKeThuHangHoa_Nam
-- =====================================================
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'uf_ThongKeThuHangHoa_Nam' AND type IN ('P', 'FN'))
    DROP PROCEDURE uf_ThongKeThuHangHoa_Nam;
GO

CREATE PROCEDURE uf_ThongKeThuHangHoa_Nam
    @idcompany INT = NULL,
    @nam INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @nam = ISNULL(@nam, YEAR(GETDATE()));
    SELECT
        YEAR(thh.Ngaythu)                AS nam,
        MONTH(thh.Ngaythu)               AS thang,
        NULL                             AS tuan,
        SUM(thh.Thanhtoan)               AS tongsotien,
        COUNT(*)                         AS soluot
    FROM dbo.TblThuHangHoa thh
    WHERE YEAR(thh.Ngaythu) = @nam
      AND (@idcompany IS NULL OR thh.Idcompany = @idcompany)
    GROUP BY YEAR(thh.Ngaythu), MONTH(thh.Ngaythu)
    ORDER BY MONTH(thh.Ngaythu);
END;
GO

-- =====================================================
-- Stored Procedure: uf_ThongKeThuHangHoa_Thang
-- =====================================================
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'uf_ThongKeThuHangHoa_Thang' AND type IN ('P', 'FN'))
    DROP PROCEDURE uf_ThongKeThuHangHoa_Thang;
GO

CREATE PROCEDURE uf_ThongKeThuHangHoa_Thang
    @idcompany INT = NULL,
    @nam INT = NULL,
    @thang INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @nam = ISNULL(@nam, YEAR(GETDATE()));
    SET @thang = ISNULL(@thang, MONTH(GETDATE()));
    SELECT
        YEAR(thh.Ngaythu)                AS nam,
        MONTH(thh.Ngaythu)               AS thang,
        DATEPART(WEEK, thh.Ngaythu)      AS tuan,
        SUM(thh.Thanhtoan)               AS tongsotien,
        COUNT(*)                         AS soluot
    FROM dbo.TblThuHangHoa thh
    WHERE YEAR(thh.Ngaythu) = @nam
      AND MONTH(thh.Ngaythu) = @thang
      AND (@idcompany IS NULL OR thh.Idcompany = @idcompany)
    GROUP BY YEAR(thh.Ngaythu), MONTH(thh.Ngaythu), DATEPART(WEEK, thh.Ngaythu)
    ORDER BY DATEPART(WEEK, thh.Ngaythu);
END;
GO

-- =====================================================
-- Stored Procedure: uf_ThongKeThuHangHoa_Tuan
-- =====================================================
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'uf_ThongKeThuHangHoa_Tuan' AND type IN ('P', 'FN'))
    DROP PROCEDURE uf_ThongKeThuHangHoa_Tuan;
GO

CREATE PROCEDURE uf_ThongKeThuHangHoa_Tuan
    @idcompany INT = NULL,
    @nam INT = NULL,
    @tuan INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @nam = ISNULL(@nam, YEAR(GETDATE()));
    SET @tuan = ISNULL(@tuan, DATEPART(WEEK, GETDATE()));
    SELECT
        YEAR(thh.Ngaythu)                AS nam,
        MONTH(thh.Ngaythu)               AS thang,
        DATEPART(WEEK, thh.Ngaythu)      AS tuan,
        SUM(thh.Thanhtoan)               AS tongsotien,
        COUNT(*)                         AS soluot
    FROM dbo.TblThuHangHoa thh
    WHERE YEAR(thh.Ngaythu) = @nam
      AND DATEPART(WEEK, thh.Ngaythu) = @tuan
      AND (@idcompany IS NULL OR thh.Idcompany = @idcompany)
    GROUP BY YEAR(thh.Ngaythu), MONTH(thh.Ngaythu), DATEPART(WEEK, thh.Ngaythu);
END;
GO

PRINT 'All objects created successfully!';
GO
