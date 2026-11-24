using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoInteligente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Rc1ProductSearchFts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE VIRTUAL TABLE ProductSearch USING fts5(productId UNINDEXED, name, brand, category, tokenize='unicode61 remove_diacritics 2');");
            migrationBuilder.Sql("INSERT INTO ProductSearch(productId, name, brand, category) SELECT Id, Name, COALESCE(Brand, ''), COALESCE(CategoryPath, '') FROM ExternalProducts;");
            migrationBuilder.Sql("CREATE TRIGGER ProductSearchInsert AFTER INSERT ON ExternalProducts BEGIN INSERT INTO ProductSearch(productId, name, brand, category) VALUES(new.Id, new.Name, COALESCE(new.Brand, ''), COALESCE(new.CategoryPath, '')); END;");
            migrationBuilder.Sql("CREATE TRIGGER ProductSearchUpdate AFTER UPDATE OF Name, Brand, CategoryPath ON ExternalProducts BEGIN DELETE FROM ProductSearch WHERE productId=old.Id; INSERT INTO ProductSearch(productId, name, brand, category) VALUES(new.Id, new.Name, COALESCE(new.Brand, ''), COALESCE(new.CategoryPath, '')); END;");
            migrationBuilder.Sql("CREATE TRIGGER ProductSearchDelete AFTER DELETE ON ExternalProducts BEGIN DELETE FROM ProductSearch WHERE productId=old.Id; END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER ProductSearchInsert; DROP TRIGGER ProductSearchUpdate; DROP TRIGGER ProductSearchDelete; DROP TABLE ProductSearch;");
        }
    }
}
