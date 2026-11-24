namespace MTL.APP
{
	public class MTLEnum
	{
		public class LoaiPhieu
		{
			private string loaiphieu = "";
			private int id = 0;
			public int Id
			{
				get { return id; }
				set { id = value; }
			}
			public string Loaiphieu
			{
				get { return loaiphieu; }
				set { loaiphieu = value; }
			}
			public LoaiPhieu(int id, string loaiphieu)
			{
				this.Id = id;
				this.Loaiphieu = loaiphieu;
			}

			public const int
				TatCa = 1,
				HoaDon = 2,
				DichVu = 3,
				BaoGia = 4,
				DatHang = 5;
		}
		public class LoaiHinhKinhDoanh
		{
			private string loaihinhkinhdoanh = "";
			private int id = 0;
			private string icon = "";
			public int Id
			{
				get { return id; }
				set { id = value; }
			}
			public string Loaihinhkinhdoanh
			{
				get { return loaihinhkinhdoanh; }
				set { loaihinhkinhdoanh = value; }
			}

			public string Icon
			{
				get { return icon; }
				set { icon = value; }
			}

			public LoaiHinhKinhDoanh(int id, string loaihinhkinhdoanh, string icon)
			{
				this.Id = id;
				this.Loaihinhkinhdoanh = loaihinhkinhdoanh;
				this.Icon = icon;
			}

			public const int
				HangHoa = 1,
				DichVu = 2,
				Combo = 3,
				PhanMem = 4,
				SmartHome = 6;
		}
		public static List<LoaiPhieu> GetListLoaiPhieu()
		{
			List<LoaiPhieu> list = new List<LoaiPhieu>();
			list.Add(new LoaiPhieu(1, "Tất cả"));
			list.Add(new LoaiPhieu(2, "Hóa đơn"));
			list.Add(new LoaiPhieu(3, "Dịch vụ"));
			list.Add(new LoaiPhieu(4, "Báo giá"));
			list.Add(new LoaiPhieu(5, "Đặt hàng"));
			return list;
		}

		public static List<LoaiHinhKinhDoanh> GetListLoaiHinhKD()
		{
			List<LoaiHinhKinhDoanh> list = new List<LoaiHinhKinhDoanh>();
			list.Add(new LoaiHinhKinhDoanh(1, "Hàng hoá", "fas fa-list"));
			list.Add(new LoaiHinhKinhDoanh(2, "Dịch vụ", "fas fa-wrench"));
			list.Add(new LoaiHinhKinhDoanh(3, "Combo", "fas fa-th"));
			//list.Add(new LoaiHinhKinhDoanh(4, "Phần mềm", "fab fa-apple"));
			//list.Add(new LoaiHinhKinhDoanh(6, "SmartHome", "fas fa-warehouse"));
			return list;
		}


		public static class TonKho
		{
			public static List<String> data
				= new List<string> { "Còn hàng", "Hết hàng", "Tất cả" };
			public const string
				TatCa = "Tất cả",
				ConHang = "Còn hàng",
				HetHang = "Hết hàng";

		}
		public static class TonKhoAPI
		{
			public static List<String> data
				= new List<string> { "TẤT CẢ", "CÒN HÀNG", "CHỜ HÀNG", "HẾT HÀNG" };
			public const string
				TatCa = "TẤT CẢ",
				ConHang = "CÒN HÀNG",
				ChoHang = "CHỜ HÀNG",
				HetHang = "HẾT HÀNG";

		}

		public static class SapXepTheoNgay
		{
			public static List<String> data
				= new List<string> { "Ngày tạo", "Giá tăng dần", "Giá giảm dần" };
			public const string
				NgayTao = "Ngày tạo",
				GiaTangDan = "Giá tăng dần",
				GiaGiamDan = "Giá giảm dần";
		}
		public static class SoDongHienThi
		{
			public static List<int> data
				= new List<int> { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 2000, 5000, 10000 };
		}

		public class TrangThaiBaiDang
		{
			private string trangThai = "";
			private int id = 0;

			public int Id
			{
				get { return id; }
				set { id = value; }
			}
			public string TrangThai
			{
				get { return trangThai; }
				set { trangThai = value; }
			}

			public TrangThaiBaiDang(int id, string trangThai)
			{
				this.Id = id;
				this.TrangThai = trangThai;
			}

			public const int

				DangXuLy = 1,
				ChoDuyet = 2,
				DaLenLich = 3,
				HoanThanh = 4;
		}
		public static List<TrangThaiBaiDang> GetListTrangThaiBaiDang()
		{
			List<TrangThaiBaiDang> list = new List<TrangThaiBaiDang>();

			list.Add(new TrangThaiBaiDang(1, "Đang xữ lý"));
			list.Add(new TrangThaiBaiDang(2, "Chờ duyệt"));
			list.Add(new TrangThaiBaiDang(3, "Đã lên lịch"));
			list.Add(new TrangThaiBaiDang(4, "Hoàn thành"));
			return list;
		}




        public class HinhThucThanhToan
        {
            private string mota = "";
            private int id = 0;
            public int Id
            {
                get { return id; }
                set { id = value; }
            }
            public string Mota
            {
                get { return mota; }
                set { mota = value; }
            }
            public HinhThucThanhToan(int id, string mota)
            {
                this.Id = id;
                this.Mota = mota;
            }

			public const int
				TienMat = 1,
				ChuyenKhoan = 2,
				Tragop = 3,
			    Quetthe = 4,
				TienMatVaChuyenKhoan = 5;
        }
        public static List<HinhThucThanhToan> GetListHinhThucThanhToan()
        {
            List<HinhThucThanhToan> list = new List<HinhThucThanhToan>();
            list.Add(new HinhThucThanhToan(1, "Tiền mặt"));
            list.Add(new HinhThucThanhToan(2, "Chuyển khoản"));
            list.Add(new HinhThucThanhToan(3, "Trả góp"));
            list.Add(new HinhThucThanhToan(4, "Quẹt thẻ"));
            list.Add(new HinhThucThanhToan(5, "Tiền mặt + CK"));
            return list;
        }

    }
}
