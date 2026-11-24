using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoInteligente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizerBasketExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Budget",
                table: "Baskets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxStores",
                table: "Baskets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreferredStrategy",
                table: "Baskets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AllowSubstitutesOverride",
                table: "BasketItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Dimension",
                table: "BasketItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExcludedBrand",
                table: "BasketItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredBrand",
                table: "BasketItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SizeTolerancePercentOverride",
                table: "BasketItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "BasketItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Budget",
                table: "Baskets");

            migrationBuilder.DropColumn(
                name: "MaxStores",
                table: "Baskets");

            migrationBuilder.DropColumn(
                name: "PreferredStrategy",
                table: "Baskets");

            migrationBuilder.DropColumn(
                name: "AllowSubstitutesOverride",
                table: "BasketItems");

            migrationBuilder.DropColumn(
                name: "Dimension",
                table: "BasketItems");

            migrationBuilder.DropColumn(
                name: "ExcludedBrand",
                table: "BasketItems");

            migrationBuilder.DropColumn(
                name: "PreferredBrand",
                table: "BasketItems");

            migrationBuilder.DropColumn(
                name: "SizeTolerancePercentOverride",
                table: "BasketItems");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "BasketItems");
        }
    }
}
