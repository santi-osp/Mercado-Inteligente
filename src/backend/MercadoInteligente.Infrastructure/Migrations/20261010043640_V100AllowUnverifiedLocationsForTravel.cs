using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoInteligente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V100AllowUnverifiedLocationsForTravel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowUnverifiedLocationsForTravel",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowUnverifiedLocationsForTravel",
                table: "Settings");
        }
    }
}
