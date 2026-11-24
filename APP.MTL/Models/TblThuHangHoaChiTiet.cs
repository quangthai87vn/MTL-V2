namespace MTL.APP.Models;

public partial class TblThuHangHoaChiTiet
{
    public int Id { get; set; }
    public int? Idphieu { get; set; }
    public string? Maphieu { get; set; }
    public string? Tensanpham { get; set; }
    public string? Motasanpham { get; set; }
    public double? Soluong { get; set; }
    public double? Dongia { get; set; }
    public double? Thanhtien { get; set; }
    public int? Idtinhtrang { get; set; }
    public string? Ghichu { get; set; }
    public DateTime? Beginupdate { get; set; }
    public DateTime? Lastupdate { get; set; }
}

public partial class MTL_GetAllThuHangHoaResult
{
    public int id { get; set; }
    public string? maphieu { get; set; }
    public DateTime? ngaythu { get; set; }
    public int? idkhachhang { get; set; }
    public string? tenkhachhang { get; set; }
    public string? sdtkhachhang { get; set; }
    public string? diachi { get; set; }
    public string? cmndcccd { get; set; }
    public double? tongtienthu { get; set; }
    public double? giamgia { get; set; }
    public double? thanhtoan { get; set; }
    public bool? isThanhToan { get; set; }
    public string? tennhanvien { get; set; }
    public string? ghichu { get; set; }
    public DateTime? beginupdate { get; set; }
    public DateTime? lastupdate { get; set; }
}

public partial class MTL_ThongKeThuHangHoaResult
{
    public int nam { get; set; }
    public int thang { get; set; }
    public int? tuan { get; set; }
    public double? tongsotien { get; set; }
    public int? soluot { get; set; }
}

public class ThuHangHoaTinhtrang
{
    public int Id { get; set; }
    public string Tentrangthai { get; set; }
}
