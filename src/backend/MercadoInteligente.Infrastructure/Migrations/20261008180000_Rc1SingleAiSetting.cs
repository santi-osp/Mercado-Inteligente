using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MercadoInteligente.Infrastructure.Migrations;

[DbContext(typeof(MarketDb))]
[Migration("20261008180000_Rc1SingleAiSetting")]
public sealed class Rc1SingleAiSetting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE Settings DROP COLUMN AiEnabled;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "AiEnabled", table: "Settings", type: "INTEGER",
            nullable: false, defaultValue: false);
    }
}
