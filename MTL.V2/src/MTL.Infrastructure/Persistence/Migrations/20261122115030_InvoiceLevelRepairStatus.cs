using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceLevelRepairStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RepairStatus",
                table: "SalesInvoices",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "SalesInvoices" AS invoice
                SET "RepairStatus" = COALESCE(
                    (
                        SELECT repair."Status"
                        FROM "RepairOrders" AS repair
                        WHERE repair."SalesInvoiceId" = invoice."Id"
                          AND repair."IsDeleted" = FALSE
                        ORDER BY repair."CreatedAtUtc"
                        LIMIT 1
                    ),
                    'Received'
                )
                WHERE invoice."Type" = 'Repair'
                  AND invoice."RepairStatus" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepairStatus",
                table: "SalesInvoices");
        }
    }
}
