select * from [dbo].[tbl_HoaDonBanHang]
where YEAR([ngaytraphieu])=2025 ---and MONTH([ngaytraphieu])=1
and tienmat=0 and tienchuyenkhoan=0 and tienchuyenkhoandukien=0
and idLoaiPhieu <>3 and thanhtoan<>0


select * from [dbo].[tbl_HoaDonBanHang]
where YEAR([ngaytraphieu])=2026 and MONTH([ngaytraphieu])=4
and tienmat=0 and tienchuyenkhoan=0 and tienchuyenkhoandukien=0
and idLoaiPhieu <>3 and thanhtoan<>0


update  [dbo].[tbl_HoaDonBanHang] set tienmat= thanhtoan
where YEAR([ngaytraphieu])=2021 -- and MONTH([ngaytraphieu])=1
and tienmat=0 and tienchuyenkhoan=0 and tienchuyenkhoandukien=0
and idLoaiPhieu <>3 and thanhtoan<>0