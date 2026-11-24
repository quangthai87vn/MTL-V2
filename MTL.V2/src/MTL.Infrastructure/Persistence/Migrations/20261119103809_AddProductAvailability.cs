using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Availability",
                table: "Products",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "OutOfStock");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Availability",
                table: "Products");
        }
    }
}
