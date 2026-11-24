namespace MTL.APP.Models;

public partial class TblThuHangHoa
{
    public int Id { get; set; }
    public string? Maphieu { get; set; }
    public DateTime? Ngaythu { get; set; }
    public int? Idkhachhang { get; set; }
    public double? Tongtienthu { get; set; }
    public double? Giamgia { get; set; }
    public double? Thanhtoan { get; set; }
    public bool? IsThanhToan { get; set; }
    public int? Idcompany { get; set; }
    public int? Idnguoithu { get; set; }
    public string? Ghichu { get; set; }
    public DateTime? Beginupdate { get; set; }
    public DateTime? Lastupdate { get; set; }
    public int? Iduserlastupdate { get; set; }
}
