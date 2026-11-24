using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallmentPaymentAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InstallmentPaymentAccountId",
                table: "SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_InstallmentPaymentAccountId",
                table: "SalesInvoices",
                column: "InstallmentPaymentAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_PaymentAccounts_InstallmentPaymentAccountId",
                table: "SalesInvoices",
                column: "InstallmentPaymentAccountId",
                principalTable: "PaymentAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_PaymentAccounts_InstallmentPaymentAccountId",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_InstallmentPaymentAccountId",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "InstallmentPaymentAccountId",
                table: "SalesInvoices");
        }
    }
}
