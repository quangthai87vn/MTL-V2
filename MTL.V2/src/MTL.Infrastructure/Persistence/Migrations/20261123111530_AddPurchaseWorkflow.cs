using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "SalesInvoices"
                SET "RepairStatus" = 'InProgress'
                WHERE "RepairStatus" IS NULL OR "RepairStatus" NOT IN ('InProgress', 'Returned', 'Cancelled');

                UPDATE "RepairOrders"
                SET "Status" = 'InProgress'
                WHERE "Status" NOT IN ('InProgress', 'Returned', 'Cancelled');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                table: "PurchaseInvoices");

            migrationBuilder.AlterColumn<string>(
                name: "TaxCode",
                table: "Suppliers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Suppliers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "Suppliers",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacySource",
                table: "Suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SupplierInvoiceNumber",
                table: "PurchaseInvoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueDateUtc",
                table: "PurchaseInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InventoryPosted",
                table: "PurchaseInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "PurchaseInvoices",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacySource",
                table: "PurchaseInvoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReceivedAtUtc",
                table: "PurchaseInvoices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "SupplierInvoiceSeries",
                table: "PurchaseInvoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehouseReceiptNumber",
                table: "PurchaseInvoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "PurchaseInvoiceLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQuantity",
                table: "PurchaseInvoiceLines",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "PurchaseInvoiceLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "UnitName",
                table: "PurchaseInvoiceLines",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "PurchaseInvoiceLines",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchasePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PaymentAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_PurchasePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchasePayments_PaymentAccounts_PaymentAccountId",
                        column: x => x.PaymentAccountId,
                        principalTable: "PaymentAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PurchasePayments_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CompanyId_LegacySource_LegacyId",
                table: "Suppliers",
                columns: new[] { "CompanyId", "LegacySource", "LegacyId" },
                unique: true,
                filter: "\"LegacySource\" IS NOT NULL AND \"LegacyId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_CompanyId_LegacySource_LegacyId",
                table: "PurchaseInvoices",
                columns: new[] { "CompanyId", "LegacySource", "LegacyId" },
                unique: true,
                filter: "\"LegacySource\" IS NOT NULL AND \"LegacyId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceLines_CompanyId_PurchaseInvoiceId",
                table: "PurchaseInvoiceLines",
                columns: new[] { "CompanyId", "PurchaseInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePayments_CompanyId_PaidAtUtc",
                table: "PurchasePayments",
                columns: new[] { "CompanyId", "PaidAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePayments_PaymentAccountId",
                table: "PurchasePayments",
                column: "PaymentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePayments_PurchaseInvoiceId",
                table: "PurchasePayments",
                column: "PurchaseInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                table: "PurchaseInvoices",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                table: "PurchaseInvoices");

            migrationBuilder.DropTable(
                name: "PurchasePayments");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CompanyId_LegacySource_LegacyId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_CompanyId_LegacySource_LegacyId",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoiceLines_CompanyId_PurchaseInvoiceId",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "LegacySource",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DueDateUtc",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "InventoryPosted",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "LegacySource",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "ReceivedAtUtc",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "SupplierInvoiceSeries",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "WarehouseReceiptNumber",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "ReceivedQuantity",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "UnitName",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "PurchaseInvoiceLines");

            migrationBuilder.AlterColumn<string>(
                name: "TaxCode",
                table: "Suppliers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Suppliers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "SupplierInvoiceNumber",
                table: "PurchaseInvoices",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                table: "PurchaseInvoices",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
