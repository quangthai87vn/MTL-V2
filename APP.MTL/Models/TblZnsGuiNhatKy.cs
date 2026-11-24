#nullable disable
using System;

namespace MTL.APP.Models;

/// <summary>Nhật ký gửi tin ZNS (webhook n8n).</summary>
public partial class TblZnsGuiNhatKy
{
	public int Id { get; set; }
	public int? Idcompany { get; set; }
	public int? Iduser { get; set; }
	public string Mahd { get; set; }
	public int? Idkhachhang { get; set; }
	public string Sodienthoai { get; set; }
	public string Tenkhachhang { get; set; }
	public int? Idtemplate { get; set; }
	public string Tenmau { get; set; }
	public string Loaidichvu { get; set; }
	public string Noidungxemtruoc { get; set; }
	public string RequestUrl { get; set; }
	public int? HttpStatus { get; set; }
	public string ResponseBody { get; set; }
	public bool Thanhcong { get; set; }
	public string Ghichu { get; set; }
	public DateTime Ngaygui { get; set; }
}
