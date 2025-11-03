using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecommerce.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatingHoursToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExceptionsJson",
                table: "Settings",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "OpeningHoursJson",
                table: "Settings",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Settings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "America/Sao_Paulo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExceptionsJson",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "OpeningHoursJson",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Settings");
        }
    }
}
