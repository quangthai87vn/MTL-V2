using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageGallery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WooMediaId = table.Column<long>(type: "bigint", nullable: true),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AltText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_CompanyId_ProductId_Position",
                table: "ProductImages",
                columns: new[] { "CompanyId", "ProductId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_CompanyId_WooMediaId",
                table: "ProductImages",
                columns: new[] { "CompanyId", "WooMediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");

            migrationBuilder.Sql(
                """
                INSERT INTO "ProductImages" (
                    "Id", "ProductId", "WooMediaId", "Url", "ThumbnailUrl", "Name", "AltText", "Position",
                    "CreatedAtUtc", "UpdatedAtUtc", "CreatedByUserId", "UpdatedByUserId", "IsDeleted", "DeletedAtUtc", "CompanyId")
                SELECT
                    gen_random_uuid(), "Id", NULL, "PrimaryImageUrl", "PrimaryImageUrl", "Name", NULL, 0,
                    "CreatedAtUtc", "UpdatedAtUtc", "CreatedByUserId", "UpdatedByUserId", false, NULL, "CompanyId"
                FROM "Products"
                WHERE NOT "IsDeleted"
                  AND "PrimaryImageUrl" IS NOT NULL
                  AND btrim("PrimaryImageUrl") <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductImages");
        }
    }
}
