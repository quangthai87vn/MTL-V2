-- Sửa lỗi: Invalid object name 'dbo.TblUser' khi gọi uf_GetAllThuHangHoa
-- Nguyên nhân: bảng thực tế là dbo.tbl_users (map EF TblUser), không có dbo.TblUser.
-- Chạy script này trên SQL Server (database đang dùng cho app).

IF EXISTS (SELECT * FROM sys.objects WHERE name = 'uf_GetAllThuHangHoa' AND type IN ('P', 'FN'))
    DROP PROCEDURE dbo.uf_GetAllThuHangHoa;
GO

CREATE PROCEDURE dbo.uf_GetAllThuHangHoa
    @idcompany INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        thh.Id                           AS id,
        thh.Maphieu                      AS maphieu,
        thh.Ngaythu                     AS ngaythu,
        thh.Idkhachhang                  AS idkhachhang,
        thh.Tenkhachhang                 AS tenkhachhang,
        thh.Sdtkhachhang                 AS sdtkhachhang,
        thh.Diachi                       AS diachi,
        thh.Cmndcccd                     AS cmndcccd,
        thh.Hinhanhcccd                  AS hinhanhcccd,
        thh.Tongtienthu                  AS tongtienthu,
        thh.Giamgia                      AS giamgia,
        thh.Thanhtoan                    AS thanhtoan,
        thh.IsThanhToan                  AS isThanhToan,
        thh.Idcompany                    AS idcompany,
        thh.Idnguoithu                   AS idnguoithu,
        thh.Ghichu                       AS ghichu,
        thh.Beginupdate                  AS beginupdate,
        thh.Lastupdate                    AS lastupdate,
        u.fullname                       AS tennhanvien
    FROM dbo.TblThuHangHoa thh
    LEFT JOIN dbo.tbl_users u ON thh.Idnguoithu = u.id
    WHERE (@idcompany IS NULL OR thh.Idcompany = @idcompany)
    ORDER BY thh.Ngaythu DESC, thh.Id DESC;
END;
GO
