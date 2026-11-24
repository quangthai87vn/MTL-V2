using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutomateInstallmentAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsScheduled",
                table: "SalesPayments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                INSERT INTO "SalesPayments" (
                    "Id", "SalesInvoiceId", "Method", "PaymentAccountId", "Amount", "PaidAtUtc",
                    "IsScheduled", "Reference", "Notes", "CreatedAtUtc", "UpdatedAtUtc",
                    "CreatedByUserId", "UpdatedByUserId", "IsDeleted", "DeletedAtUtc", "CompanyId")
                SELECT
                    gen_random_uuid(), i."Id", 'BankTransfer', i."InstallmentPaymentAccountId",
                    i."TotalAmount" - i."PaidAmount", i."PaymentDueDateUtc", TRUE,
                    i."InstallmentReference",
                    CASE
                        WHEN NULLIF(BTRIM(i."InstallmentProvider"), '') IS NULL THEN 'Khoản tiền góp dự kiến'
                        ELSE 'Đơn vị trả góp: ' || i."InstallmentProvider"
                    END,
                    NOW(), NOW(), i."CreatedByUserId", i."UpdatedByUserId", FALSE, NULL, i."CompanyId"
                FROM "SalesInvoices" i
                WHERE i."IsDeleted" = FALSE
                  AND i."IsInstallment" = TRUE
                  AND i."TotalAmount" > i."PaidAmount"
                  AND i."InstallmentPaymentAccountId" IS NOT NULL
                  AND i."PaymentDueDateUtc" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "SalesPayments" p
                      WHERE p."SalesInvoiceId" = i."Id"
                        AND p."IsDeleted" = FALSE
                        AND p."IsScheduled" = TRUE
                  );

                UPDATE "SalesInvoices" i
                SET "PaidAmount" = i."TotalAmount",
                    "Status" = 'Paid',
                    "UpdatedAtUtc" = NOW()
                WHERE i."IsDeleted" = FALSE
                  AND i."IsInstallment" = TRUE
                  AND EXISTS (
                      SELECT 1
                      FROM "SalesPayments" p
                      WHERE p."SalesInvoiceId" = i."Id"
                        AND p."IsDeleted" = FALSE
                        AND p."IsScheduled" = TRUE
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "SalesInvoices" i
                SET "PaidAmount" = COALESCE((
                        SELECT SUM(p."Amount")
                        FROM "SalesPayments" p
                        WHERE p."SalesInvoiceId" = i."Id"
                          AND p."IsDeleted" = FALSE
                          AND p."IsScheduled" = FALSE
                    ), 0),
                    "Status" = CASE
                        WHEN COALESCE((
                            SELECT SUM(p."Amount")
                            FROM "SalesPayments" p
                            WHERE p."SalesInvoiceId" = i."Id"
                              AND p."IsDeleted" = FALSE
                              AND p."IsScheduled" = FALSE
                        ), 0) >= i."TotalAmount" THEN 'Paid'
                        WHEN COALESCE((
                            SELECT SUM(p."Amount")
                            FROM "SalesPayments" p
                            WHERE p."SalesInvoiceId" = i."Id"
                              AND p."IsDeleted" = FALSE
                              AND p."IsScheduled" = FALSE
                        ), 0) > 0 THEN 'PartiallyPaid'
                        ELSE 'Confirmed'
                    END,
                    "UpdatedAtUtc" = NOW()
                WHERE EXISTS (
                    SELECT 1
                    FROM "SalesPayments" p
                    WHERE p."SalesInvoiceId" = i."Id"
                      AND p."IsDeleted" = FALSE
                      AND p."IsScheduled" = TRUE
                );

                DELETE FROM "SalesPayments"
                WHERE "IsScheduled" = TRUE;
                """);

            migrationBuilder.DropColumn(
                name: "IsScheduled",
                table: "SalesPayments");
        }
    }
}
