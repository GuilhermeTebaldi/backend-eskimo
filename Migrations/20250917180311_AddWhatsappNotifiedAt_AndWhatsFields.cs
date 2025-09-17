using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecommerce.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsappNotifiedAt_AndWhatsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WhatsappAccessToken",
                table: "PaymentConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsappPhoneNumberId",
                table: "PaymentConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsappStoreNumber",
                table: "PaymentConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsappNotifiedAt",
                table: "Orders",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WhatsappAccessToken",
                table: "PaymentConfigs");

            migrationBuilder.DropColumn(
                name: "WhatsappPhoneNumberId",
                table: "PaymentConfigs");

            migrationBuilder.DropColumn(
                name: "WhatsappStoreNumber",
                table: "PaymentConfigs");

            migrationBuilder.DropColumn(
                name: "WhatsappNotifiedAt",
                table: "Orders");
        }
    }
}
