# Database design

PostgreSQL, 31 tables. Names follow the original ER diagram: singular `snake_case` tables,
`<table>_id` primary keys, money as integer cents. The source of truth is the entity classes in
`backend/src/Plushies.Api/Domain` and `Data/PlushiesDbContext.cs`; EF Core migrations build the schema.

## Answer to the note on the diagram

> "Ask Kye if we want the plushies to have their own type or each type recorded by colours —
> I have 10 blue Mollys, or just 10 Mollys? Could Blue Molly be more expensive than Black Molly?"

Both are covered. **Stock and price live on `product_variant`**, not on `product`. "Molly the Bear"
is one product; "Blueberry", "Midnight" and "Honey (limited)" are variants, each with its own
`price_cents`, `stock`, `sku` and look. So you can hold 10 blue and 4 black, and charge more for
one colour (the demo data does: Honey is $42, the others $36).

## From the diagram, unchanged in meaning

`brand`, `category`, `product`, `product_category`, `product_variant_type`, `category_variant`,
`product_variant`, `product_variant_image`, `promotion`, `product_variant_promotion`, `review`,
`wish_list`, `customer`, `cart`, `cart_item`, `orders`, `order_item`, `users`, `post`, `post_type`,
`store_page`, `store_text_info`, `store_image_type`, `store_image_info`.

## What was added

| Table | Why |
|---|---|
| `customer_address` | A customer can keep several addresses, split into the fields Chit Chats needs (`address_1`, `address_2`, `province_code`, `country_code`). |
| `custom_design` | A plushie built in the builder: the design (jsonb), size, embroidery, notes, and the price the server calculated. |
| `coupon` | The diagram had `coupon_code` on the order but nowhere to define a coupon. Percent or fixed, minimum spend, date range, redemption limit. |
| `payment` | One row per charge against an order: provider, reference, amount, status. |
| `shipping_method` | What the customer picks and pays at checkout, per country, with an optional free-shipping threshold and a preferred Chit Chats `postage_type`. |
| `tax_rate` | Sales tax by country and province. |
| `shipment` | The local record of a Chit Chats shipment: their id and status, postage, tracking, label URLs, parcel size, and the last rates returned. |

Columns added to existing tables:

- `product`: `slug` (URLs), `tags` (text[]), `hs_tariff_code` and `origin_country` (customs), `updated_at`.
- `product_variant`: `sku`, `weight_grams` (shipping), `color_hex`, `design` (jsonb, how the plushie is drawn), `active`.
- `promotion`: `discount_percent` — the diagram had dates and display options but no discount amount.
- `cart`: `session_token` so guests can have a cart before signing in; `coupon_id`.
- `cart_item` / `order_item`: `product_variant_id` and `custom_design_id`. A line is a variant *or* a custom design (enforced by a check constraint on `cart_item`).
- `orders`: `order_number`, `currency`, `shipping_cents`, `tax_cents`, `tax_label`, the shipping method, a **copy of the ship-to address and email**, `customer_note`, `paid_at`, `updated_at`.
- `order_item`: copies of the product name, variant, SKU, design, weight and price at the time of purchase, so editing or deleting a product never rewrites past orders.
- `category`: `slug`. `post`: `slug`, `excerpt`, `published`. `store_image_info`: `display_order`. `users`: `display_name`.

## What was changed from the diagram

| Diagram | Now | Reason |
|---|---|---|
| table `order` | `orders` | `order` is a reserved word in PostgreSQL; every hand-written query would need quotes. |
| table `user` | `users` | Same: `user` is reserved. |
| table `reviews` | `review` | Singular, like every other table. |
| `customer.address`, `country`, `province`, `city`, `postal_code` | moved to `customer_address` | One address per customer is too few, and a single `address` column can't be sent to Chit Chats. |
| `product.tag` | `product.tags` (text[]) | A product can have more than one tag. |
| `product_category.primary` | `is_primary` | `primary` is a reserved word. |
| `product_variant.price_cent` | `price_cents` | Matches `subtotal_cents`, `total_cents`, etc. |
| `cart_item.count` | `quantity` | Matches `order_item.quantity`. |
| `cart.coupon_discount` | `cart.coupon_id` | The discount is calculated from the coupon each time, so it can't go stale when the cart changes. |
| `promotion.promotion_title` etc. | `title`, `description`, `start_date`, … | Dropped the repeated `promotion_` prefix. |
| `customer.password`, `users.password` | same column, stores a **hash** | Passwords are never stored in plain text (PBKDF2 via ASP.NET Core's `PasswordHasher`). |

## Rules the database enforces

- Unique: `customer.email`, `users.email`, `product.slug`, `category.slug`, `product_variant.sku`,
  `coupon.code`, `orders.order_number`, `shipment.chit_chats_shipment_id`, one review and one
  wishlist entry per customer per product, one text per (`store_page_id`, `text_code`).
- Checks: variant price and stock are never negative, rating is 1–5, promotion discount is 0–100,
  cart quantity is positive.
- Stock is taken with a guarded `UPDATE … WHERE stock >= quantity` inside the checkout
  transaction, so two shoppers can't both buy the last one.
- Deleting a product that has been ordered hides it instead (order history keeps its link).

## Status values

- `orders.status`: `pending_payment`, `paid`, `in_production`, `shipped`, `delivered`, `cancelled`, `refunded`.
- `payment.status`: `pending`, `succeeded`, `failed`, `refunded`.
- `shipment.status`: Chit Chats' own values (`pending`, `ready`, `received`, `released`, `inducted`,
  `in_transit`, `delivered`, `exception`, `voided`, …) stored as they arrive.
