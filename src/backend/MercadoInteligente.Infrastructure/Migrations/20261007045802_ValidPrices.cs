using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoInteligente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ValidPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_PriceObservation_ValidPrice",
                table: "PriceObservations",
                sql: "CAST(Price AS NUMERIC) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Offer_ValidPrice",
                table: "Offers",
                sql: "CAST(Price AS NUMERIC) > 0 AND (ListPrice IS NULL OR CAST(ListPrice AS NUMERIC) > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PriceObservation_ValidPrice",
                table: "PriceObservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Offer_ValidPrice",
                table: "Offers");
        }
    }
}
