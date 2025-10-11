using Microsoft.EntityFrameworkCore.Migrations;
namespace ecommerce.Migrations
{
    public partial class AddVisibilityLayoutColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortRank",
                table: "StoreProductVisibilities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PinnedTop",
                table: "StoreProductVisibilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StyleJson",
                table: "StoreProductVisibilities",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SortRank", table: "StoreProductVisibilities");
            migrationBuilder.DropColumn(name: "PinnedTop", table: "StoreProductVisibilities");
            migrationBuilder.DropColumn(name: "StyleJson", table: "StoreProductVisibilities");
        }
    }
}
