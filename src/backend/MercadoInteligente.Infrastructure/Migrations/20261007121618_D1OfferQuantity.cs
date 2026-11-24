using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoInteligente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class D1OfferQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AvailableQuantityRaw",
                table: "Offers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableQuantityRaw",
                table: "Offers");
        }
    }
}
