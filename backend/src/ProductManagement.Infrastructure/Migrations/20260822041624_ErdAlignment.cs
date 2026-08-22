using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProductManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ErdAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.CreateTable(
                name: "product_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    qty_in_stock = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    product_image = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_items", x => x.id);
                    table.CheckConstraint("ck_product_items_price", "price >= 0");
                    table.CheckConstraint("ck_product_items_qty", "qty_in_stock >= 0");
                    table.ForeignKey(
                        name: "FK_product_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promotion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    discount_rate = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion", x => x.id);
                    table.CheckConstraint("ck_promotion_dates", "end_date >= start_date");
                    table.CheckConstraint("ck_promotion_discount_rate", "discount_rate > 0 AND discount_rate <= 1");
                });

            migrationBuilder.CreateTable(
                name: "variation",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_variation", x => x.id);
                    table.ForeignKey(
                        name: "FK_variation_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promotion_category",
                columns: table => new
                {
                    promotion_id = table.Column<long>(type: "bigint", nullable: false),
                    category_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion_category", x => new { x.promotion_id, x.category_id });
                    table.ForeignKey(
                        name: "FK_promotion_category_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promotion_category_promotion_promotion_id",
                        column: x => x.promotion_id,
                        principalTable: "promotion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "variation_option",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    variation_id = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_variation_option", x => x.id);
                    table.ForeignKey(
                        name: "FK_variation_option_variation_variation_id",
                        column: x => x.variation_id,
                        principalTable: "variation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_configuration",
                columns: table => new
                {
                    product_item_id = table.Column<long>(type: "bigint", nullable: false),
                    variation_option_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_configuration", x => new { x.product_item_id, x.variation_option_id });
                    table.ForeignKey(
                        name: "FK_product_configuration_product_items_product_item_id",
                        column: x => x.product_item_id,
                        principalTable: "product_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_configuration_variation_option_variation_option_id",
                        column: x => x.variation_option_id,
                        principalTable: "variation_option",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_configuration_variation_option_id",
                table: "product_configuration",
                column: "variation_option_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_items_active_in_stock",
                table: "product_items",
                column: "product_id",
                filter: "is_active = true AND qty_in_stock > 0");

            migrationBuilder.CreateIndex(
                name: "IX_product_items_sku",
                table: "product_items",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_promotion_category_category_id",
                table: "promotion_category",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_variation_category_id_name",
                table: "variation",
                columns: new[] { "category_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_variation_option_variation_id_value",
                table: "variation_option",
                columns: new[] { "variation_id", "value" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_configuration");

            migrationBuilder.DropTable(
                name: "promotion_category");

            migrationBuilder.DropTable(
                name: "product_items");

            migrationBuilder.DropTable(
                name: "variation_option");

            migrationBuilder.DropTable(
                name: "promotion");

            migrationBuilder.DropTable(
                name: "variation");

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    color = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    compare_at_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    stock_quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.id);
                    table.CheckConstraint("ck_product_variants_compare_at_price", "compare_at_price IS NULL OR compare_at_price >= price");
                    table.CheckConstraint("ck_product_variants_price", "price >= 0");
                    table.CheckConstraint("ck_product_variants_stock", "stock_quantity >= 0");
                    table.ForeignKey(
                        name: "FK_product_variants_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_active_in_stock",
                table: "product_variants",
                column: "product_id",
                filter: "is_active AND stock_quantity > 0");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_sku",
                table: "product_variants",
                column: "sku",
                unique: true);
        }
    }
}
