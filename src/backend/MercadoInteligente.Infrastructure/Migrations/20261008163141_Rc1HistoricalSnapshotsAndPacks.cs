using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoInteligente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Rc1HistoricalSnapshotsAndPacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowOutdatedOffers",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AvailableQuantity",
                table: "PriceObservations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ListPrice",
                table: "PriceObservations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceContext",
                table: "PriceObservations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceScope",
                table: "PriceObservations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionConditions",
                table: "PriceObservations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SellerId",
                table: "PriceObservations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "PriceObservations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "LocationProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Neighborhood",
                table: "LocationProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Number",
                table: "LocationProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "LocationProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "LocationProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "LocationProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttributesJson",
                table: "ExternalSkus",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PackageCount",
                table: "ExternalSkus",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityPerUnit",
                table: "ExternalSkus",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalQuantity",
                table: "ExternalSkus",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "ExternalSkus",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowOutdatedOffers",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "AvailableQuantity",
                table: "PriceObservations");

            migrationBuilder.DropColumn(
                name: "ListPrice",
                table: "PriceObservations");

            migrationBuilder.DropColumn(
                name: "PriceContext",
                table: "PriceObservations");

            migrationBuilder.DropColumn(
                name: "PriceScope",
                table: "PriceObservations");

            migrationBuilder.DropColumn(
                name: "PromotionConditions",
                table: "PriceObservations");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "PriceObservations");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "PriceObservations");

            migrationBuilder.DropColumn(
                name: "City",
                table: "LocationProfiles");

            migrationBuilder.DropColumn(
                name: "Neighborhood",
                table: "LocationProfiles");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "LocationProfiles");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "LocationProfiles");

            migrationBuilder.DropColumn(
                name: "State",
                table: "LocationProfiles");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "LocationProfiles");

            migrationBuilder.DropColumn(
                name: "AttributesJson",
                table: "ExternalSkus");

            migrationBuilder.DropColumn(
                name: "PackageCount",
                table: "ExternalSkus");

            migrationBuilder.DropColumn(
                name: "QuantityPerUnit",
                table: "ExternalSkus");

            migrationBuilder.DropColumn(
                name: "TotalQuantity",
                table: "ExternalSkus");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "ExternalSkus");
        }
    }
}
