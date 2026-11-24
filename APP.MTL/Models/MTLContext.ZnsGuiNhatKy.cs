// Mở rộng DbContext: bảng nhật ký ZNS (không ghi đè file MTLContext auto-generated).
#nullable disable
using Microsoft.EntityFrameworkCore;

namespace MTL.APP.Models;

public partial class MTLContext
{
	public virtual DbSet<TblZnsGuiNhatKy> TblZnsGuiNhatKies { get; set; }

	partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<TblZnsGuiNhatKy>(entity =>
		{
			entity.ToTable("tbl_ZnsGuiNhatKy");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id).HasColumnName("Id");
			entity.Property(e => e.Idcompany).HasColumnName("Idcompany");
			entity.Property(e => e.Iduser).HasColumnName("Iduser");
			entity.Property(e => e.Mahd).HasMaxLength(30).HasColumnName("Mahd");
			entity.Property(e => e.Idkhachhang).HasColumnName("Idkhachhang");
			entity.Property(e => e.Sodienthoai).HasMaxLength(50).HasColumnName("Sodienthoai");
			entity.Property(e => e.Tenkhachhang).HasMaxLength(200).HasColumnName("Tenkhachhang");
			entity.Property(e => e.Idtemplate).HasColumnName("Idtemplate");
			entity.Property(e => e.Tenmau).HasMaxLength(500).HasColumnName("Tenmau");
			entity.Property(e => e.Loaidichvu).HasMaxLength(100).HasColumnName("Loaidichvu");
			entity.Property(e => e.Noidungxemtruoc).HasColumnName("Noidungxemtruoc");
			entity.Property(e => e.RequestUrl).HasMaxLength(2000).HasColumnName("RequestUrl");
			entity.Property(e => e.HttpStatus).HasColumnName("HttpStatus");
			entity.Property(e => e.ResponseBody).HasColumnName("ResponseBody");
			entity.Property(e => e.Thanhcong).HasColumnName("Thanhcong");
			entity.Property(e => e.Ghichu).HasMaxLength(1000).HasColumnName("Ghichu");
			entity.Property(e => e.Ngaygui).HasColumnType("datetime2").HasColumnName("Ngaygui");
		});
	}
}
