using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerAddress",
                table: "SalesInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                table: "SalesInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstallmentProvider",
                table: "SalesInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstallmentReference",
                table: "SalesInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInstallment",
                table: "SalesInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaymentDueDateUtc",
                table: "SalesInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PromotionDiscountAmount",
                table: "SalesInvoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PromotionId",
                table: "SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionName",
                table: "SalesInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "SalesInvoices",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Sale");

            migrationBuilder.AddColumn<string>(
                name: "VoucherCode",
                table: "SalesInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VoucherDiscountAmount",
                table: "SalesInvoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "VoucherId",
                table: "SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "SalesInvoiceLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitName",
                table: "SalesInvoiceLines",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Cái");

            migrationBuilder.CreateTable(
                name: "PaymentAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BankName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountHolder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DiscountType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumOrderAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaximumDiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepairOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceModel = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Accessories = table.Column<string>(type: "text", nullable: true),
                    DeviceCondition = table.Column<string>(type: "text", nullable: false),
                    CustomerRequest = table.Column<string>(type: "text", nullable: false),
                    TechnicianAssessment = table.Column<string>(type: "text", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedReturnAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepairOrders_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DiscountType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumOrderAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaximumDiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UsageLimit = table.Column<int>(type: "integer", nullable: true),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PaymentAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesPayments_PaymentAccounts_PaymentAccountId",
                        column: x => x.PaymentAccountId,
                        principalTable: "PaymentAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesPayments_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAccounts_CompanyId_IsActive_DisplayOrder",
                table: "PaymentAccounts",
                columns: new[] { "CompanyId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_CompanyId_IsActive",
                table: "Promotions",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrders_SalesInvoiceId",
                table: "RepairOrders",
                column: "SalesInvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayments_CompanyId_PaidAtUtc",
                table: "SalesPayments",
                columns: new[] { "CompanyId", "PaidAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayments_PaymentAccountId",
                table: "SalesPayments",
                column: "PaymentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayments_SalesInvoiceId",
                table: "SalesPayments",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_CompanyId_Code",
                table: "Vouchers",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Promotions");

            migrationBuilder.DropTable(
                name: "RepairOrders");

            migrationBuilder.DropTable(
                name: "SalesPayments");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "PaymentAccounts");

            migrationBuilder.DropColumn(
                name: "CustomerAddress",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "InstallmentProvider",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "InstallmentReference",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "IsInstallment",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentDueDateUtc",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "PromotionDiscountAmount",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "PromotionId",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "PromotionName",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "VoucherCode",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "VoucherDiscountAmount",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "VoucherId",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "SalesInvoiceLines");

            migrationBuilder.DropColumn(
                name: "UnitName",
                table: "SalesInvoiceLines");
        }
    }
}
