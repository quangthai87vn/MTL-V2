using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPublishingMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CatalogVisibility",
                table: "Products",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Visible");

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "Products",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatalogVisibility",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                table: "Products");
        }
    }
}
