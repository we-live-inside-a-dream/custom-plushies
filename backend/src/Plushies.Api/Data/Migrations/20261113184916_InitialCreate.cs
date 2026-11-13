using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Plushies.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brand",
                columns: table => new
                {
                    brand_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brand", x => x.brand_id);
                });

            migrationBuilder.CreateTable(
                name: "category",
                columns: table => new
                {
                    category_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    parent_category_id = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_category", x => x.category_id);
                    table.ForeignKey(
                        name: "fk_category_category_parent_category_id",
                        column: x => x.parent_category_id,
                        principalTable: "category",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coupon",
                columns: table => new
                {
                    coupon_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    discount_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    discount_value = table.Column<int>(type: "integer", nullable: false),
                    min_subtotal_cents = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    max_redemptions = table.Column<int>(type: "integer", nullable: true),
                    times_redeemed = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coupon", x => x.coupon_id);
                });

            migrationBuilder.CreateTable(
                name: "customer",
                columns: table => new
                {
                    customer_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "text", nullable: false),
                    password = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer", x => x.customer_id);
                });

            migrationBuilder.CreateTable(
                name: "post_type",
                columns: table => new
                {
                    post_type_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    post_type_title = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_post_type", x => x.post_type_id);
                });

            migrationBuilder.CreateTable(
                name: "product_variant_type",
                columns: table => new
                {
                    product_variant_type_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_variant_type", x => x.product_variant_type_id);
                });

            migrationBuilder.CreateTable(
                name: "promotion",
                columns: table => new
                {
                    promotion_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    discount_percent = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    max_display_number = table.Column<int>(type: "integer", nullable: false),
                    display_home_page = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotion", x => x.promotion_id);
                    table.CheckConstraint("ck_promotion_discount", "discount_percent BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateTable(
                name: "shipping_method",
                columns: table => new
                {
                    shipping_method_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    free_over_cents = table.Column<int>(type: "integer", nullable: true),
                    estimated_days = table.Column<string>(type: "text", nullable: true),
                    chit_chats_postage_type = table.Column<string>(type: "text", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipping_method", x => x.shipping_method_id);
                });

            migrationBuilder.CreateTable(
                name: "store_image_type",
                columns: table => new
                {
                    store_image_type_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    max_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_image_type", x => x.store_image_type_id);
                });

            migrationBuilder.CreateTable(
                name: "store_page",
                columns: table => new
                {
                    store_page_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    store_page_name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_page", x => x.store_page_id);
                });

            migrationBuilder.CreateTable(
                name: "tax_rate",
                columns: table => new
                {
                    tax_rate_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    province_code = table.Column<string>(type: "text", nullable: true),
                    label = table.Column<string>(type: "text", nullable: false),
                    rate_percent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tax_rate", x => x.tax_rate_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "text", nullable: false),
                    password = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "product",
                columns: table => new
                {
                    product_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    brand_id = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    information = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    hs_tariff_code = table.Column<string>(type: "text", nullable: true),
                    origin_country = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product", x => x.product_id);
                    table.ForeignKey(
                        name: "fk_product_brand_brand_id",
                        column: x => x.brand_id,
                        principalTable: "brand",
                        principalColumn: "brand_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cart",
                columns: table => new
                {
                    cart_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_id = table.Column<int>(type: "integer", nullable: true),
                    session_token = table.Column<string>(type: "text", nullable: true),
                    coupon_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart", x => x.cart_id);
                    table.ForeignKey(
                        name: "fk_cart_coupons_coupon_id",
                        column: x => x.coupon_id,
                        principalTable: "coupon",
                        principalColumn: "coupon_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_cart_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "customer_id");
                });

            migrationBuilder.CreateTable(
                name: "custom_design",
                columns: table => new
                {
                    custom_design_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_id = table.Column<int>(type: "integer", nullable: true),
                    session_token = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    design = table.Column<string>(type: "jsonb", nullable: false),
                    size = table.Column<string>(type: "text", nullable: false),
                    embroidery_text = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    weight_grams = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_design", x => x.custom_design_id);
                    table.ForeignKey(
                        name: "fk_custom_design_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "customer_id");
                });

            migrationBuilder.CreateTable(
                name: "customer_address",
                columns: table => new
                {
                    customer_address_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_id = table.Column<int>(type: "integer", nullable: false),
                    recipient_name = table.Column<string>(type: "text", nullable: false),
                    address1 = table.Column<string>(type: "text", nullable: false),
                    address2 = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: false),
                    province_code = table.Column<string>(type: "text", nullable: true),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_address", x => x.customer_address_id);
                    table.ForeignKey(
                        name: "fk_customer_address_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "category_variant",
                columns: table => new
                {
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    product_variant_type_id = table.Column<int>(type: "integer", nullable: false),
                    field = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_category_variant", x => new { x.category_id, x.product_variant_type_id });
                    table.ForeignKey(
                        name: "fk_category_variant_category_category_id",
                        column: x => x.category_id,
                        principalTable: "category",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_category_variant_product_variant_types_product_variant_type",
                        column: x => x.product_variant_type_id,
                        principalTable: "product_variant_type",
                        principalColumn: "product_variant_type_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    order_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_number = table.Column<string>(type: "text", nullable: false),
                    customer_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    subtotal_cents = table.Column<int>(type: "integer", nullable: false),
                    discount_cents = table.Column<int>(type: "integer", nullable: false),
                    shipping_cents = table.Column<int>(type: "integer", nullable: false),
                    tax_cents = table.Column<int>(type: "integer", nullable: false),
                    total_cents = table.Column<int>(type: "integer", nullable: false),
                    coupon_code = table.Column<string>(type: "text", nullable: true),
                    tax_label = table.Column<string>(type: "text", nullable: true),
                    shipping_method_id = table.Column<int>(type: "integer", nullable: true),
                    shipping_method_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    ship_name = table.Column<string>(type: "text", nullable: false),
                    ship_address1 = table.Column<string>(type: "text", nullable: false),
                    ship_address2 = table.Column<string>(type: "text", nullable: true),
                    ship_city = table.Column<string>(type: "text", nullable: false),
                    ship_province_code = table.Column<string>(type: "text", nullable: true),
                    ship_postal_code = table.Column<string>(type: "text", nullable: true),
                    ship_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ship_phone = table.Column<string>(type: "text", nullable: true),
                    customer_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.order_id);
                    table.ForeignKey(
                        name: "fk_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_shipping_methods_shipping_method_id",
                        column: x => x.shipping_method_id,
                        principalTable: "shipping_method",
                        principalColumn: "shipping_method_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "post",
                columns: table => new
                {
                    post_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    post_type_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    excerpt = table.Column<string>(type: "text", nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    banner_image = table.Column<string>(type: "text", nullable: true),
                    published = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_post", x => x.post_id);
                    table.ForeignKey(
                        name: "fk_post_post_types_post_type_id",
                        column: x => x.post_type_id,
                        principalTable: "post_type",
                        principalColumn: "post_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_post_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "store_image_info",
                columns: table => new
                {
                    store_image_info_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    store_page_id = table.Column<int>(type: "integer", nullable: false),
                    store_image_type_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: true),
                    intro = table.Column<string>(type: "text", nullable: true),
                    link = table.Column<string>(type: "text", nullable: true),
                    link_text = table.Column<string>(type: "text", nullable: true),
                    image_address = table.Column<string>(type: "text", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_image_info", x => x.store_image_info_id);
                    table.ForeignKey(
                        name: "fk_store_image_info_store_image_types_store_image_type_id",
                        column: x => x.store_image_type_id,
                        principalTable: "store_image_type",
                        principalColumn: "store_image_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_store_image_info_store_pages_store_page_id",
                        column: x => x.store_page_id,
                        principalTable: "store_page",
                        principalColumn: "store_page_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_store_image_info_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "store_text_info",
                columns: table => new
                {
                    store_text_info_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    store_page_id = table.Column<int>(type: "integer", nullable: false),
                    text_code = table.Column<string>(type: "text", nullable: false),
                    text_value = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_text_info", x => x.store_text_info_id);
                    table.ForeignKey(
                        name: "fk_store_text_info_store_page_store_page_id",
                        column: x => x.store_page_id,
                        principalTable: "store_page",
                        principalColumn: "store_page_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_store_text_info_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "product_category",
                columns: table => new
                {
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_category", x => new { x.category_id, x.product_id });
                    table.ForeignKey(
                        name: "fk_product_category_category_category_id",
                        column: x => x.category_id,
                        principalTable: "category",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_category_product_product_id",
                        column: x => x.product_id,
                        principalTable: "product",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_variant",
                columns: table => new
                {
                    product_variant_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_variant_type_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    product_variant_value = table.Column<string>(type: "text", nullable: false),
                    sku = table.Column<string>(type: "text", nullable: false),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    stock = table.Column<int>(type: "integer", nullable: false),
                    weight_grams = table.Column<int>(type: "integer", nullable: false),
                    color_hex = table.Column<string>(type: "text", nullable: true),
                    design = table.Column<string>(type: "jsonb", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_variant", x => x.product_variant_id);
                    table.CheckConstraint("ck_product_variant_price", "price_cents >= 0");
                    table.CheckConstraint("ck_product_variant_stock", "stock >= 0");
                    table.ForeignKey(
                        name: "fk_product_variant_product_product_id",
                        column: x => x.product_id,
                        principalTable: "product",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_variant_product_variant_types_product_variant_type_",
                        column: x => x.product_variant_type_id,
                        principalTable: "product_variant_type",
                        principalColumn: "product_variant_type_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review",
                columns: table => new
                {
                    review_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    customer_id = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review", x => x.review_id);
                    table.CheckConstraint("ck_review_rating", "rating BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_review_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_product_product_id",
                        column: x => x.product_id,
                        principalTable: "product",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wish_list",
                columns: table => new
                {
                    wishlist_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    customer_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wish_list", x => x.wishlist_id);
                    table.ForeignKey(
                        name: "fk_wish_list_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_wish_list_product_product_id",
                        column: x => x.product_id,
                        principalTable: "product",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment",
                columns: table => new
                {
                    payment_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    provider_reference = table.Column<string>(type: "text", nullable: true),
                    amount_cents = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment", x => x.payment_id);
                    table.ForeignKey(
                        name: "fk_payment_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipment",
                columns: table => new
                {
                    shipment_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    chit_chats_shipment_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    postage_type = table.Column<string>(type: "text", nullable: true),
                    carrier = table.Column<string>(type: "text", nullable: true),
                    carrier_tracking_code = table.Column<string>(type: "text", nullable: true),
                    tracking_url = table.Column<string>(type: "text", nullable: true),
                    postage_cost_cents = table.Column<int>(type: "integer", nullable: true),
                    package_type = table.Column<string>(type: "text", nullable: false),
                    weight_grams = table.Column<int>(type: "integer", nullable: false),
                    size_x_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    size_y_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    size_z_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    label_png_url = table.Column<string>(type: "text", nullable: true),
                    label_pdf_url = table.Column<string>(type: "text", nullable: true),
                    label_zpl_url = table.Column<string>(type: "text", nullable: true),
                    batch_id = table.Column<string>(type: "text", nullable: true),
                    ship_date = table.Column<DateOnly>(type: "date", nullable: true),
                    rates_json = table.Column<string>(type: "jsonb", nullable: true),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipment", x => x.shipment_id);
                    table.ForeignKey(
                        name: "fk_shipment_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cart_item",
                columns: table => new
                {
                    cart_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cart_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: true),
                    product_variant_id = table.Column<int>(type: "integer", nullable: true),
                    custom_design_id = table.Column<int>(type: "integer", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_item", x => x.cart_item_id);
                    table.CheckConstraint("ck_cart_item_quantity", "quantity > 0");
                    table.CheckConstraint("ck_cart_item_target", "(product_variant_id IS NULL) <> (custom_design_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_cart_item_cart_cart_id",
                        column: x => x.cart_id,
                        principalTable: "cart",
                        principalColumn: "cart_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cart_item_custom_designs_custom_design_id",
                        column: x => x.custom_design_id,
                        principalTable: "custom_design",
                        principalColumn: "custom_design_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cart_item_product_variants_product_variant_id",
                        column: x => x.product_variant_id,
                        principalTable: "product_variant",
                        principalColumn: "product_variant_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cart_item_products_product_id",
                        column: x => x.product_id,
                        principalTable: "product",
                        principalColumn: "product_id");
                });

            migrationBuilder.CreateTable(
                name: "order_item",
                columns: table => new
                {
                    order_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: true),
                    product_variant_id = table.Column<int>(type: "integer", nullable: true),
                    custom_design_id = table.Column<int>(type: "integer", nullable: true),
                    product_name = table.Column<string>(type: "text", nullable: false),
                    variant_name = table.Column<string>(type: "text", nullable: true),
                    sku = table.Column<string>(type: "text", nullable: true),
                    design = table.Column<string>(type: "jsonb", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price_cents = table.Column<int>(type: "integer", nullable: false),
                    total_cents = table.Column<int>(type: "integer", nullable: false),
                    weight_grams = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_item", x => x.order_item_id);
                    table.ForeignKey(
                        name: "fk_order_item_custom_design_custom_design_id",
                        column: x => x.custom_design_id,
                        principalTable: "custom_design",
                        principalColumn: "custom_design_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_order_item_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_order_item_product_variants_product_variant_id",
                        column: x => x.product_variant_id,
                        principalTable: "product_variant",
                        principalColumn: "product_variant_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_order_item_products_product_id",
                        column: x => x.product_id,
                        principalTable: "product",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "product_variant_image",
                columns: table => new
                {
                    product_image_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_variant_id = table.Column<int>(type: "integer", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: false),
                    image_thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_variant_image", x => x.product_image_id);
                    table.ForeignKey(
                        name: "fk_product_variant_image_product_variant_product_variant_id",
                        column: x => x.product_variant_id,
                        principalTable: "product_variant",
                        principalColumn: "product_variant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_variant_promotion",
                columns: table => new
                {
                    product_variant_id = table.Column<int>(type: "integer", nullable: false),
                    promotion_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_variant_promotion", x => new { x.product_variant_id, x.promotion_id });
                    table.ForeignKey(
                        name: "fk_product_variant_promotion_product_variant_product_variant_id",
                        column: x => x.product_variant_id,
                        principalTable: "product_variant",
                        principalColumn: "product_variant_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_variant_promotion_promotions_promotion_id",
                        column: x => x.promotion_id,
                        principalTable: "promotion",
                        principalColumn: "promotion_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_brand_name",
                table: "brand",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cart_coupon_id",
                table: "cart",
                column: "coupon_id");

            migrationBuilder.CreateIndex(
                name: "ix_cart_customer_id",
                table: "cart",
                column: "customer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cart_session_token",
                table: "cart",
                column: "session_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cart_item_cart_id",
                table: "cart_item",
                column: "cart_id");

            migrationBuilder.CreateIndex(
                name: "ix_cart_item_custom_design_id",
                table: "cart_item",
                column: "custom_design_id");

            migrationBuilder.CreateIndex(
                name: "ix_cart_item_product_id",
                table: "cart_item",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_cart_item_product_variant_id",
                table: "cart_item",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_category_parent_category_id",
                table: "category",
                column: "parent_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_category_slug",
                table: "category",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_category_variant_product_variant_type_id",
                table: "category_variant",
                column: "product_variant_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_coupon_code",
                table: "coupon",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_custom_design_customer_id",
                table: "custom_design",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_email",
                table: "customer",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_address_customer_id",
                table: "customer_address",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_custom_design_id",
                table: "order_item",
                column: "custom_design_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_order_id",
                table: "order_item",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_product_id",
                table: "order_item",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_product_variant_id",
                table: "order_item",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_created_at",
                table: "orders",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_id",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_order_number",
                table: "orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_shipping_method_id",
                table: "orders",
                column: "shipping_method_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_order_id",
                table: "payment",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_post_post_type_id",
                table: "post",
                column: "post_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_post_slug",
                table: "post",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_post_user_id",
                table: "post",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_brand_id",
                table: "product",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_slug",
                table: "product",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_category_product_id",
                table: "product_category",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variant_product_id",
                table: "product_variant",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variant_product_variant_type_id",
                table: "product_variant",
                column: "product_variant_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variant_sku",
                table: "product_variant",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_variant_image_product_variant_id",
                table: "product_variant_image",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variant_promotion_promotion_id",
                table: "product_variant_promotion",
                column: "promotion_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_customer_id",
                table: "review",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_product_id_customer_id",
                table: "review",
                columns: new[] { "product_id", "customer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipment_chit_chats_shipment_id",
                table: "shipment",
                column: "chit_chats_shipment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipment_order_id",
                table: "shipment",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_image_info_store_image_type_id",
                table: "store_image_info",
                column: "store_image_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_image_info_store_page_id",
                table: "store_image_info",
                column: "store_page_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_image_info_user_id",
                table: "store_image_info",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_page_store_page_name",
                table: "store_page",
                column: "store_page_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_text_info_store_page_id_text_code",
                table: "store_text_info",
                columns: new[] { "store_page_id", "text_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_text_info_user_id",
                table: "store_text_info",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tax_rate_country_code_province_code",
                table: "tax_rate",
                columns: new[] { "country_code", "province_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wish_list_customer_id_product_id",
                table: "wish_list",
                columns: new[] { "customer_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wish_list_product_id",
                table: "wish_list",
                column: "product_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_item");

            migrationBuilder.DropTable(
                name: "category_variant");

            migrationBuilder.DropTable(
                name: "customer_address");

            migrationBuilder.DropTable(
                name: "order_item");

            migrationBuilder.DropTable(
                name: "payment");

            migrationBuilder.DropTable(
                name: "post");

            migrationBuilder.DropTable(
                name: "product_category");

            migrationBuilder.DropTable(
                name: "product_variant_image");

            migrationBuilder.DropTable(
                name: "product_variant_promotion");

            migrationBuilder.DropTable(
                name: "review");

            migrationBuilder.DropTable(
                name: "shipment");

            migrationBuilder.DropTable(
                name: "store_image_info");

            migrationBuilder.DropTable(
                name: "store_text_info");

            migrationBuilder.DropTable(
                name: "tax_rate");

            migrationBuilder.DropTable(
                name: "wish_list");

            migrationBuilder.DropTable(
                name: "cart");

            migrationBuilder.DropTable(
                name: "custom_design");

            migrationBuilder.DropTable(
                name: "post_type");

            migrationBuilder.DropTable(
                name: "category");

            migrationBuilder.DropTable(
                name: "product_variant");

            migrationBuilder.DropTable(
                name: "promotion");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "store_image_type");

            migrationBuilder.DropTable(
                name: "store_page");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "coupon");

            migrationBuilder.DropTable(
                name: "product");

            migrationBuilder.DropTable(
                name: "product_variant_type");

            migrationBuilder.DropTable(
                name: "customer");

            migrationBuilder.DropTable(
                name: "shipping_method");

            migrationBuilder.DropTable(
                name: "brand");
        }
    }
}
