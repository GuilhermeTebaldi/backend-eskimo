using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecommerce.Migrations
{
    /// <inheritdoc />
    public partial class AddIndex_Promotions_ProductId_IsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Promotions_ProductId",
                table: "Promotions");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_ProductId_IsActive",
                table: "Promotions",
                columns: new[] { "ProductId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Promotions_ProductId_IsActive",
                table: "Promotions");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_ProductId",
                table: "Promotions",
                column: "ProductId");
        }
    }
}
