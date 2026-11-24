using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoInteligente.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Baskets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Baskets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CanonicalProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Brand = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanonicalProducts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComparisonProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    MaxStores = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComparisonProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocationProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    Latitude = table.Column<decimal>(type: "TEXT", nullable: true),
                    Longitude = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHealth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderHealth", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RawCaptures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawCaptures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Retailers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Retailers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MaxStores = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeTolerancePercent = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowSubstitutes = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxOverbuyPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    PriceTtlHours = table.Column<int>(type: "INTEGER", nullable: false),
                    StockTtlHours = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductTtlDays = table.Column<int>(type: "INTEGER", nullable: false),
                    StoresTtlDays = table.Column<int>(type: "INTEGER", nullable: false),
                    RouteTtlHours = table.Column<int>(type: "INTEGER", nullable: false),
                    AiEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BasketItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BasketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Query = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BasketItems_Baskets_BasketId",
                        column: x => x.BasketId,
                        principalTable: "Baskets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanonicalProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ean = table.Column<string>(type: "TEXT", nullable: true),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    Dimension = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariants_CanonicalProducts_CanonicalProductId",
                        column: x => x.CanonicalProductId,
                        principalTable: "CanonicalProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RetailerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Brand = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalProducts_Retailers_RetailerId",
                        column: x => x.RetailerId,
                        principalTable: "Retailers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sellers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RetailerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sellers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sellers_Retailers_RetailerId",
                        column: x => x.RetailerId,
                        principalTable: "Retailers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RetailerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Latitude = table.Column<decimal>(type: "TEXT", nullable: true),
                    Longitude = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stores_Retailers_RetailerId",
                        column: x => x.RetailerId,
                        principalTable: "Retailers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalSkus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    Ean = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    Dimension = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalSkus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalSkus_ExternalProducts_ExternalProductId",
                        column: x => x.ExternalProductId,
                        principalTable: "ExternalProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalSkuId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchOverrides_ExternalSkus_ExternalSkuId",
                        column: x => x.ExternalSkuId,
                        principalTable: "ExternalSkus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchOverrides_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalSkuId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SellerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StoreId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    ListPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    Availability = table.Column<int>(type: "INTEGER", nullable: false),
                    Context = table.Column<string>(type: "TEXT", nullable: false),
                    PromotionConditions = table.Column<string>(type: "TEXT", nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RawCaptureId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Offers_ExternalSkus_ExternalSkuId",
                        column: x => x.ExternalSkuId,
                        principalTable: "ExternalSkus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Offers_RawCaptures_RawCaptureId",
                        column: x => x.RawCaptureId,
                        principalTable: "RawCaptures",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Offers_Sellers_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Sellers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Offers_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalSkuId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Evidence = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductMatches_ExternalSkus_ExternalSkuId",
                        column: x => x.ExternalSkuId,
                        principalTable: "ExternalSkus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductMatches_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfferId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Availability = table.Column<int>(type: "INTEGER", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RawCaptureId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceObservations_Offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BasketItems_BasketId",
                table: "BasketItems",
                column: "BasketId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalProducts_RetailerId_ExternalId",
                table: "ExternalProducts",
                columns: new[] { "RetailerId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalSkus_Ean",
                table: "ExternalSkus",
                column: "Ean");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalSkus_ExternalProductId_ExternalId",
                table: "ExternalSkus",
                columns: new[] { "ExternalProductId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchOverrides_ExternalSkuId",
                table: "MatchOverrides",
                column: "ExternalSkuId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchOverrides_ProductVariantId",
                table: "MatchOverrides",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_ExternalSkuId_SellerId_Context",
                table: "Offers",
                columns: new[] { "ExternalSkuId", "SellerId", "Context" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Offers_RawCaptureId",
                table: "Offers",
                column: "RawCaptureId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_SellerId",
                table: "Offers",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_StoreId",
                table: "Offers",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceObservations_OfferId_CheckedAt",
                table: "PriceObservations",
                columns: new[] { "OfferId", "CheckedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductMatches_ExternalSkuId",
                table: "ProductMatches",
                column: "ExternalSkuId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductMatches_ProductVariantId",
                table: "ProductMatches",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_CanonicalProductId",
                table: "ProductVariants",
                column: "CanonicalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_Ean",
                table: "ProductVariants",
                column: "Ean");

            migrationBuilder.CreateIndex(
                name: "IX_RawCaptures_Sha256",
                table: "RawCaptures",
                column: "Sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Retailers_Code",
                table: "Retailers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sellers_RetailerId_ExternalId",
                table: "Sellers",
                columns: new[] { "RetailerId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_RetailerId_ExternalId",
                table: "Stores",
                columns: new[] { "RetailerId", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BasketItems");

            migrationBuilder.DropTable(
                name: "ComparisonProfiles");

            migrationBuilder.DropTable(
                name: "LocationProfiles");

            migrationBuilder.DropTable(
                name: "MatchOverrides");

            migrationBuilder.DropTable(
                name: "PriceObservations");

            migrationBuilder.DropTable(
                name: "ProductMatches");

            migrationBuilder.DropTable(
                name: "ProviderHealth");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "SyncRuns");

            migrationBuilder.DropTable(
                name: "Baskets");

            migrationBuilder.DropTable(
                name: "Offers");

            migrationBuilder.DropTable(
                name: "ProductVariants");

            migrationBuilder.DropTable(
                name: "ExternalSkus");

            migrationBuilder.DropTable(
                name: "RawCaptures");

            migrationBuilder.DropTable(
                name: "Sellers");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "CanonicalProducts");

            migrationBuilder.DropTable(
                name: "ExternalProducts");

            migrationBuilder.DropTable(
                name: "Retailers");
        }
    }
}
