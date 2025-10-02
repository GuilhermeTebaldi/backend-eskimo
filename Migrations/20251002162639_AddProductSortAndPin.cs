using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecommerce.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSortAndPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PinnedTop",
                table: "Products",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortRank",
                table: "Products",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PinnedTop",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SortRank",
                table: "Products");
        }
    }
}
