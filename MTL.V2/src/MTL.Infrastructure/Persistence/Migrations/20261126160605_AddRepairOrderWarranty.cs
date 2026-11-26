using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRepairOrderWarranty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WarrantyPeriod",
                table: "RepairOrders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "3 tháng");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WarrantyPeriod",
                table: "RepairOrders");
        }
    }
}
