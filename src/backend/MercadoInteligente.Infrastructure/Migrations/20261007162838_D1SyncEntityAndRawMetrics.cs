using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoInteligente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class D1SyncEntityAndRawMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductsCreated",
                table: "SyncRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProductsUpdated",
                table: "SyncRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RawDeduplicated",
                table: "SyncRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RawNew",
                table: "SyncRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SkusCreated",
                table: "SyncRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SkusUpdated",
                table: "SyncRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductsCreated",
                table: "SyncRuns");

            migrationBuilder.DropColumn(
                name: "ProductsUpdated",
                table: "SyncRuns");

            migrationBuilder.DropColumn(
                name: "RawDeduplicated",
                table: "SyncRuns");

            migrationBuilder.DropColumn(
                name: "RawNew",
                table: "SyncRuns");

            migrationBuilder.DropColumn(
                name: "SkusCreated",
                table: "SyncRuns");

            migrationBuilder.DropColumn(
                name: "SkusUpdated",
                table: "SyncRuns");
        }
    }
}
