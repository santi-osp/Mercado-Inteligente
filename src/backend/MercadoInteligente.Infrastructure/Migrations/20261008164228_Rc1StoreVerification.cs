using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoInteligente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Rc1StoreVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "Stores",
                type: "TEXT",
                nullable: false,
                defaultValue: "UNKNOWN");
            migrationBuilder.Sql("UPDATE Stores SET VerificationStatus = 'DEMO' WHERE ExternalId IN ('D1-LAURELES','D1-POBLADO','D1-BELEN','D1-CENTRO','D1-ENVIGADO','EXITO-LAURELES','EXITO-POBLADO','EXITO-COLOMBIA','EXITO-CENTRO','EXITO-ENVIGADO','ARA-LAURELES','ARA-BELEN','ARA-CENTRO');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "Stores");
        }
    }
}
