using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRepairInvoiceWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "RepairOrderServiceTypes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DefaultRepairTurnaroundDays",
                table: "Companies",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.Sql(
                """
                UPDATE "RepairOrderServiceTypes" link
                SET "Amount" = repair."EstimatedCost"
                FROM "RepairOrders" repair
                WHERE link."RepairOrderId" = repair."Id"
                  AND link."IsDeleted" = FALSE
                  AND link."Id" = (
                      SELECT first_link."Id"
                      FROM "RepairOrderServiceTypes" first_link
                      WHERE first_link."RepairOrderId" = repair."Id"
                        AND first_link."IsDeleted" = FALSE
                      ORDER BY first_link."CreatedAtUtc", first_link."Id"
                      LIMIT 1
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "RepairOrderServiceTypes");

            migrationBuilder.DropColumn(
                name: "DefaultRepairTurnaroundDays",
                table: "Companies");
        }
    }
}
