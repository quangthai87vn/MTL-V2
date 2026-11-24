using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHierarchicalWooCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductCategoryAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WooCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
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
                    table.PrimaryKey("PK_ProductCategoryAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductCategoryAssignments_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WooCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WooCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Slug = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ParentWooCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    ProductCount = table.Column<int>(type: "integer", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_WooCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategoryAssignments_CompanyId_ProductId_WooCategoryId",
                table: "ProductCategoryAssignments",
                columns: new[] { "CompanyId", "ProductId", "WooCategoryId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategoryAssignments_CompanyId_WooCategoryId",
                table: "ProductCategoryAssignments",
                columns: new[] { "CompanyId", "WooCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategoryAssignments_ProductId",
                table: "ProductCategoryAssignments",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WooCategories_CompanyId_ParentWooCategoryId",
                table: "WooCategories",
                columns: new[] { "CompanyId", "ParentWooCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_WooCategories_CompanyId_WooCategoryId",
                table: "WooCategories",
                columns: new[] { "CompanyId", "WooCategoryId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductCategoryAssignments");

            migrationBuilder.DropTable(
                name: "WooCategories");
        }
    }
}
