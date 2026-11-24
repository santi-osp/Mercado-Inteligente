using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoInteligente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Rc2LocationProfileAndOfferContextIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "LocationProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "LocationProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "LocationProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "FormattedAddress",
                table: "LocationProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateCode",
                table: "LocationProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "LocationProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Offers_CheckedAt",
                table: "Offers",
                column: "CheckedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_Scope_Context",
                table: "Offers",
                columns: new[] { "Scope", "Context" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationProfiles_Label",
                table: "LocationProfiles",
                column: "Label");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Offers_CheckedAt",
                table: "Offers");

            migrationBuilder.DropIndex(
                name: "IX_Offers_Scope_Context",
                table: "Offers");

            migrationBuilder.DropIndex(
                name: "IX_LocationProfiles_Label",
                table: "LocationProfiles");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "LocationProfiles");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "LocationProfiles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "LocationProfiles");

            migrationBuilder.DropColumn(
                name: "FormattedAddress",
                table: "LocationProfiles");

            migrationBuilder.DropColumn(
                name: "StateCode",
                table: "LocationProfiles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "LocationProfiles");
        }
    }
}
