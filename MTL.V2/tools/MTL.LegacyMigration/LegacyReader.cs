using System.Data;
using Microsoft.Data.SqlClient;

namespace MTL.LegacyMigration;

internal sealed class LegacyReader(string connectionString, int sourceCompanyId)
{
    public async Task<LegacyDataset> ReadAsync(bool includeArchive, CancellationToken cancellationToken)
    {
        var data = new LegacyDataset();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await ReadCustomers(connection, data, cancellationToken);
        await ReadInvoices(connection, data, cancellationToken);
        await ReadInvoiceLines(connection, data, cancellationToken);
        await ReadTags(connection, data, cancellationToken);
        await ReadPurchases(connection, data, cancellationToken);
        await ReadPurchaseLines(connection, data, cancellationToken);
        if (includeArchive)
        {
            await ReadArchiveInvoices(connection, data, cancellationToken);
            await ReadArchiveLines(connection, data, cancellationToken);
        }

        return data;
    }

    private async Task ReadCustomers(SqlConnection connection, LegacyDataset data, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, makh, fullname, diachi, dienthoai, masothue, ghichu, email, beginupdate, lastupdate
            FROM tbl_users WITH (NOLOCK)
            WHERE idcompany = @companyId AND idnhomkhachhang = 1
            ORDER BY id;
            """;
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("@companyId", sourceCompanyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            data.Customers.Add(new LegacyCustomer(
                reader.GetInt("id"),
                reader.GetStringOrNull("makh"),
                reader.GetStringOrNull("fullname") ?? $"Khách hàng cũ #{reader.GetInt("id")}",
                reader.GetStringOrNull("diachi"),
                reader.GetStringOrNull("dienthoai"),
                reader.GetStringOrNull("masothue"),
                reader.GetStringOrNull("ghichu"),
                reader.GetStringOrNull("email"),
                reader.GetDateTimeOrNull("beginupdate"),
                reader.GetDateTimeOrNull("lastupdate")));
        }
    }

    private async Task ReadInvoices(SqlConnection connection, LegacyDataset data, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT h.mahd, h.idkhachhang, h.idLoaiPhieu, h.ngaylapphieu, h.ngaytraphieu,
                   h.ghichu, h.ghichukohienbill, h.tongtien,
                   COALESCE(h.tienck, 0) + COALESCE(h.giamgia, 0) AS discountAmount,
                   COALESCE(h.tienvat, 0) AS taxAmount,
                   COALESCE(h.tongtiensauvat, h.tongtien, 0) AS totalAmount,
                   COALESCE(h.isDichVuDaXuLy, 0) AS isProcessed,
                   h.serial, h.model,
                   u.fullname AS customerName, u.dienthoai AS customerPhone, u.email AS customerEmail, u.diachi AS customerAddress
            FROM tbl_HoaDonBanHang h WITH (NOLOCK)
            LEFT JOIN tbl_users u WITH (NOLOCK) ON u.id = h.idkhachhang
            WHERE h.idcompany = @companyId AND h.idLoaiPhieu IN (2, 3, 4, 5)
            ORDER BY h.ngaylapphieu, h.mahd;
            """;
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("@companyId", sourceCompanyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var number = reader.GetStringOrNull("mahd") ?? throw new InvalidDataException("Hóa đơn nguồn không có mã.");
            data.Invoices.Add(new LegacyInvoice(
                number,
                reader.GetIntOrNull("idkhachhang"),
                reader.GetIntOrNull("idLoaiPhieu") ?? 2,
                reader.GetDateTimeOrNull("ngaylapphieu") ?? throw new InvalidDataException($"Hóa đơn {number} không có ngày lập."),
                reader.GetDateTimeOrNull("ngaytraphieu"),
                reader.GetStringOrNull("ghichu"),
                reader.GetStringOrNull("ghichukohienbill"),
                reader.GetDecimalValue("tongtien"),
                reader.GetDecimalValue("discountAmount"),
                reader.GetDecimalValue("taxAmount"),
                reader.GetDecimalValue("totalAmount"),
                reader.GetBooleanValue("isProcessed"),
                reader.GetStringOrNull("serial"),
                reader.GetStringOrNull("model"),
                reader.GetStringOrNull("customerName"),
                reader.GetStringOrNull("customerPhone"),
                reader.GetStringOrNull("customerEmail"),
                reader.GetStringOrNull("customerAddress")));
        }
    }

    private async Task ReadInvoiceLines(SqlConnection connection, LegacyDataset data, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT d.id, d.mahd, d.idsanpham, COALESCE(NULLIF(sp.masp, ''), NULLIF(sp.maspnew, '')) AS sku,
                   COALESCE(NULLIF(d.tensp, ''), NULLIF(sp.tensp, ''), N'Nội dung hóa đơn cũ') AS productName,
                   COALESCE(d.sl, 1) AS quantity, COALESCE(d.dongia, 0) AS unitPrice,
                   COALESCE(d.chietkhau, 0) AS discountAmount, COALESCE(d.thanhtien, 0) AS lineTotal,
                   d.ghichu AS notes, bh.baohanh AS warranty
            FROM tbl_HoaDonBanHangChiTiet d WITH (NOLOCK)
            INNER JOIN tbl_HoaDonBanHang h WITH (NOLOCK) ON h.mahd = d.mahd AND h.idcompany = @companyId
            LEFT JOIN tbl_sanpham sp WITH (NOLOCK) ON sp.id = d.idsanpham
            LEFT JOIN tbl_BaoHanh bh WITH (NOLOCK) ON bh.id = d.idbaohanh
            WHERE h.idLoaiPhieu IN (2, 3, 4, 5)
            ORDER BY d.id;
            """;
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("@companyId", sourceCompanyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            data.InvoiceLines.Add(new LegacyInvoiceLine(
                reader.GetInt("id"),
                reader.GetStringOrNull("mahd")!,
                reader.GetIntOrNull("idsanpham"),
                reader.GetStringOrNull("sku"),
                reader.GetStringOrNull("productName") ?? "Nội dung hóa đơn cũ",
                reader.GetDecimalValue("quantity", 1),
                reader.GetDecimalValue("unitPrice"),
                reader.GetDecimalValue("discountAmount"),
                reader.GetDecimalValue("lineTotal"),
                reader.GetStringOrNull("notes"),
                reader.GetStringOrNull("warranty")));
        }
    }

    private async Task ReadTags(SqlConnection connection, LegacyDataset data, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT t.id, t.name
            FROM Tag t WITH (NOLOCK)
            INNER JOIN tbl_HoaDonBanHangTagMap m WITH (NOLOCK) ON m.tagid = t.id
            INNER JOIN tbl_HoaDonBanHang h WITH (NOLOCK) ON h.mahd = m.mahd
            WHERE h.idcompany = @companyId AND h.idLoaiPhieu IN (2, 3, 5)
            ORDER BY t.id;

            SELECT m.mahd, m.tagid
            FROM tbl_HoaDonBanHangTagMap m WITH (NOLOCK)
            INNER JOIN tbl_HoaDonBanHang h WITH (NOLOCK) ON h.mahd = m.mahd
            WHERE h.idcompany = @companyId AND h.idLoaiPhieu IN (2, 3, 5)
            ORDER BY m.mahd, m.tagid;
            """;
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("@companyId", sourceCompanyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            data.Tags.Add(new LegacyTag(reader.GetInt("id"), reader.GetStringOrNull("name") ?? $"Dịch vụ cũ {reader.GetInt("id")}"));

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            data.InvoiceTags.Add(new LegacyInvoiceTag(reader.GetStringOrNull("mahd")!, reader.GetInt("tagid")));
    }

    private async Task ReadPurchases(SqlConnection connection, LegacyDataset data, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT h.id, h.mahd, h.idkhachhang, h.ngaylapphieu, h.ghichu, h.ghichukohienbill,
                   COALESCE(h.tongtien, 0) AS totalAmount, COALESCE(h.giamgia, 0) AS discountAmount,
                   COALESCE(h.thanhtoan, 0) AS paidAmount, h.hoadonvat,
                   u.fullname AS supplierName, u.dienthoai AS supplierPhone, u.email AS supplierEmail,
                   u.diachi AS supplierAddress, u.masothue AS supplierTaxCode
            FROM tbl_HoaDonNhapHang h WITH (NOLOCK)
            LEFT JOIN tbl_users u WITH (NOLOCK) ON u.id = h.idkhachhang
            WHERE h.idcompany = @companyId
            ORDER BY h.ngaylapphieu, h.mahd;
            """;
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("@companyId", sourceCompanyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var number = reader.GetStringOrNull("mahd") ?? throw new InvalidDataException("Phiếu nhập nguồn không có mã.");
            data.Purchases.Add(new LegacyPurchase(
                reader.GetInt("id"), number, reader.GetIntOrNull("idkhachhang"),
                reader.GetDateTimeOrNull("ngaylapphieu") ?? throw new InvalidDataException($"Phiếu nhập {number} không có ngày lập."),
                reader.GetStringOrNull("ghichu"), reader.GetStringOrNull("ghichukohienbill"),
                reader.GetDecimalValue("totalAmount"), reader.GetDecimalValue("discountAmount"),
                reader.GetDecimalValue("paidAmount"), reader.GetStringOrNull("hoadonvat"),
                reader.GetStringOrNull("supplierName"), reader.GetStringOrNull("supplierPhone"),
                reader.GetStringOrNull("supplierEmail"), reader.GetStringOrNull("supplierAddress"),
                reader.GetStringOrNull("supplierTaxCode")));
        }
    }

    private async Task ReadPurchaseLines(SqlConnection connection, LegacyDataset data, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT d.id, d.mahd, d.idsanpham, COALESCE(NULLIF(sp.masp, ''), NULLIF(sp.maspnew, '')) AS sku,
                   COALESCE(NULLIF(d.tensp, ''), NULLIF(sp.tensp, ''), N'Nội dung nhập hàng cũ') AS productName,
                   COALESCE(d.sl, 1) AS quantity, dv.donvitinh AS unitName,
                   COALESCE(d.dongia, 0) AS unitCost, COALESCE(d.chietkhau, 0) AS discountAmount,
                   COALESCE(d.thanhtien, 0) AS lineTotal, d.ghichu AS notes
            FROM tbl_HoaDonNhapHangChiTiet d WITH (NOLOCK)
            INNER JOIN tbl_HoaDonNhapHang h WITH (NOLOCK) ON h.mahd = d.mahd AND h.idcompany = @companyId
            LEFT JOIN tbl_sanpham sp WITH (NOLOCK) ON sp.id = d.idsanpham
            LEFT JOIN tbl_donvitinh dv WITH (NOLOCK) ON dv.id = sp.iddonvitinh
            ORDER BY d.id;
            """;
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("@companyId", sourceCompanyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            data.PurchaseLines.Add(new LegacyPurchaseLine(
                reader.GetInt("id"), reader.GetStringOrNull("mahd")!, reader.GetIntOrNull("idsanpham"),
                reader.GetStringOrNull("sku"), reader.GetStringOrNull("productName") ?? "Nội dung nhập hàng cũ",
                reader.GetDecimalValue("quantity", 1), reader.GetStringOrNull("unitName"),
                reader.GetDecimalValue("unitCost"), reader.GetDecimalValue("discountAmount"),
                reader.GetDecimalValue("lineTotal"), reader.GetStringOrNull("notes")));
        }
    }

    private static async Task ReadArchiveInvoices(SqlConnection connection, LegacyDataset data, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ID, COALESCE(NULLIF(TenKH, ''), N'Khách hàng cũ') AS customerName,
                   COALESCE(NgayBan, DateInsert) AS invoiceDate, GhiChu,
                   COALESCE(BanRa, 0) AS totalAmount, COALESCE(IsSold, 1) AS isProcessed
            FROM tblHoaDon WITH (NOLOCK)
            WHERE COALESCE(NgayBan, DateInsert) IS NOT NULL
            ORDER BY COALESCE(NgayBan, DateInsert), ID;
            """;
        await using var command = CreateCommand(connection, sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            data.ArchiveInvoices.Add(new ArchiveInvoice(
                reader.GetInt("ID"),
                reader.GetStringOrNull("customerName") ?? "Khách hàng cũ",
                reader.GetDateTimeOrNull("invoiceDate")!.Value,
                reader.GetStringOrNull("GhiChu"),
                reader.GetDecimalValue("totalAmount"),
                reader.GetBooleanValue("isProcessed")));
        }
    }

    private static async Task ReadArchiveLines(SqlConnection connection, LegacyDataset data, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ID, IDHoaDon, COALESCE(NULLIF(MatHang, ''), N'Nội dung hóa đơn cũ') AS productName,
                   DonViTinh, COALESCE(SL, 1) AS quantity, COALESCE(DonGiaBan, 0) AS unitPrice,
                   COALESCE(SL, 1) * COALESCE(DonGiaBan, 0) AS lineTotal, BaoHanh
            FROM tblHoaDonChiTiet WITH (NOLOCK)
            ORDER BY ID;
            """;
        await using var command = CreateCommand(connection, sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            data.ArchiveInvoiceLines.Add(new ArchiveInvoiceLine(
                reader.GetInt("ID"),
                reader.GetIntOrNull("IDHoaDon") ?? 0,
                reader.GetStringOrNull("productName") ?? "Nội dung hóa đơn cũ",
                reader.GetStringOrNull("DonViTinh"),
                reader.GetDecimalValue("quantity", 1),
                reader.GetDecimalValue("unitPrice"),
                reader.GetDecimalValue("lineTotal"),
                reader.GetStringOrNull("BaoHanh")));
        }
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string commandText) => new(commandText, connection)
    {
        CommandTimeout = 120
    };
}

internal static class SqlDataReaderExtensions
{
    public static int GetInt(this SqlDataReader reader, string name) => Convert.ToInt32(reader[name]);
    public static int? GetIntOrNull(this SqlDataReader reader, string name) => reader[name] is DBNull ? null : Convert.ToInt32(reader[name]);
    public static string? GetStringOrNull(this SqlDataReader reader, string name) => reader[name] is DBNull ? null : Convert.ToString(reader[name])?.Trim();
    public static DateTime? GetDateTimeOrNull(this SqlDataReader reader, string name) => reader[name] is DBNull ? null : Convert.ToDateTime(reader[name]);
    public static decimal GetDecimalValue(this SqlDataReader reader, string name, decimal fallback = 0) => reader[name] is DBNull ? fallback : Convert.ToDecimal(reader[name]);
    public static bool GetBooleanValue(this SqlDataReader reader, string name) => reader[name] is not DBNull && Convert.ToBoolean(reader[name]);
}
