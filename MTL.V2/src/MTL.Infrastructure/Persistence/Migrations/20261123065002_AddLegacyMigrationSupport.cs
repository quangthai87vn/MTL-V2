using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyMigrationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "SalesInvoices",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacySource",
                table: "SalesInvoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CitizenId",
                table: "Customers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "Customers",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacySource",
                table: "Customers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_CompanyId_LegacySource_LegacyId",
                table: "SalesInvoices",
                columns: new[] { "CompanyId", "LegacySource", "LegacyId" },
                unique: true,
                filter: "\"LegacySource\" IS NOT NULL AND \"LegacyId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId_CitizenId",
                table: "Customers",
                columns: new[] { "CompanyId", "CitizenId" },
                filter: "\"CitizenId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId_LegacySource_LegacyId",
                table: "Customers",
                columns: new[] { "CompanyId", "LegacySource", "LegacyId" },
                unique: true,
                filter: "\"LegacySource\" IS NOT NULL AND \"LegacyId\" IS NOT NULL AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_CompanyId_LegacySource_LegacyId",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CompanyId_CitizenId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CompanyId_LegacySource_LegacyId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "LegacySource",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "CitizenId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LegacySource",
                table: "Customers");
        }
    }
}
