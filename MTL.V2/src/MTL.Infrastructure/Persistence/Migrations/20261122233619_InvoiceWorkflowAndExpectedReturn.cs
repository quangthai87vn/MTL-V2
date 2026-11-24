using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceWorkflowAndExpectedReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpectedReturnAtUtc",
                table: "SalesInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "SalesInvoices" AS invoice
                SET "ExpectedReturnAtUtc" = (
                    SELECT MAX(repair."ExpectedReturnAtUtc")
                    FROM "RepairOrders" AS repair
                    WHERE repair."SalesInvoiceId" = invoice."Id"
                      AND repair."IsDeleted" = FALSE
                )
                WHERE invoice."Type" = 'Repair'
                  AND invoice."ExpectedReturnAtUtc" IS NULL;

                UPDATE "SalesInvoices"
                SET "RepairStatus" = 'Returned'
                WHERE "Type" = 'Sale'
                  AND "RepairStatus" IS NULL;

                UPDATE "SalesInvoices"
                SET "RepairStatus" = 'Received'
                WHERE "Type" = 'Repair'
                  AND "RepairStatus" IS NULL;

                UPDATE "Customers" AS customer
                SET "TotalSpent" = COALESCE((
                        SELECT SUM(invoice."TotalAmount")
                        FROM "SalesInvoices" AS invoice
                        WHERE invoice."CustomerId" = customer."Id"
                          AND invoice."RepairStatus" = 'Returned'
                          AND invoice."IsDeleted" = FALSE
                    ), 0),
                    "LastPurchaseAtUtc" = (
                        SELECT MAX(invoice."InvoiceDateUtc")
                        FROM "SalesInvoices" AS invoice
                        WHERE invoice."CustomerId" = customer."Id"
                          AND invoice."RepairStatus" = 'Returned'
                          AND invoice."IsDeleted" = FALSE
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedReturnAtUtc",
                table: "SalesInvoices");
        }
    }
}
