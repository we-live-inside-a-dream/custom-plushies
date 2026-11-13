using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Plushies.Api.Domain;

namespace Plushies.Tests;

[Collection("app")]
public class StorefrontTests(AppFixture app)
{
    private static readonly object Dino = new
    {
        species = "dino", body = "#36E0B0", belly = "#FFF1D6", eyes = "button", mouth = "smile", cheeks = true,
        accessory = "crown", accessoryColor = "#FFD84D",
    };

    private async Task<int> VariantIdAsync(string sku) =>
        await app.DbAsync(db => db.ProductVariants.Where(v => v.Sku == sku).Select(v => v.ProductVariantId).SingleAsync());

    private async Task<int> StockAsync(int variantId) =>
        await app.DbAsync(db => db.ProductVariants.Where(v => v.ProductVariantId == variantId).Select(v => v.Stock).SingleAsync());

    private static object Checkout(string token = "tok_test_ok", int shippingMethodId = 1, string country = "CA", string? province = "ON") =>
        new { address = AppFixture.Address(country, province), shippingMethodId, paymentToken = token };

    [Fact]
    public async Task Catalogue_lists_products_with_promotion_pricing()
    {
        var page = await app.Anonymous().GetJsonAsync("/api/products?sort=name");
        Assert.Equal(8, page.GetProperty("total").GetInt32());

        var molly = (await app.Anonymous().GetJsonAsync("/api/products/molly-the-bear")).GetProperty("summary");
        var blueberry = molly.GetProperty("variants").EnumerateArray().First(v => v.GetProperty("value").GetString() == "Blueberry");
        Assert.Equal(3600, blueberry.GetProperty("priceCents").GetInt32());
        Assert.Equal(3060, blueberry.GetProperty("salePriceCents").GetInt32()); // 15% promotion
        var honey = molly.GetProperty("variants").EnumerateArray().First(v => v.GetProperty("value").GetString()!.StartsWith("Honey"));
        Assert.Equal(4200, honey.GetProperty("salePriceCents").GetInt32()); // a variant can cost more, and isn't in the promo
    }

    [Fact]
    public async Task Guest_cart_follows_the_customer_through_sign_up_and_checkout()
    {
        var variantId = await VariantIdAsync("BISCUIT-004");
        var before = await StockAsync(variantId);

        var guest = app.Anonymous();
        var cart = await guest.PostOkAsync("/api/cart/items", new { productVariantId = variantId, quantity = 2 });
        var cartToken = cart.GetProperty("token").GetString()!;
        Assert.Equal(7600, cart.GetProperty("subtotalCents").GetInt32());

        var customer = await app.NewCustomerAsync(cartToken);
        var merged = await customer.GetJsonAsync("/api/cart");
        Assert.Equal(2, merged.GetProperty("itemCount").GetInt32());

        var totals = await customer.GetJsonAsync("/api/checkout/totals?country=CA&province=ON&shippingMethodId=1");
        Assert.Equal(950, totals.GetProperty("shippingCents").GetInt32());
        Assert.Equal(1112, totals.GetProperty("taxCents").GetInt32()); // 13% HST on 76.00 + 9.50, rounded
        Assert.Equal(9662, totals.GetProperty("totalCents").GetInt32());

        var response = await customer.PostAsJsonAsync("/api/checkout", Checkout());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.JsonAsync();
        Assert.Equal("paid", order.GetProperty("status").GetString());
        Assert.Equal(9662, order.GetProperty("totalCents").GetInt32());
        Assert.StartsWith("ST-", order.GetProperty("orderNumber").GetString());

        Assert.Equal(before - 2, await StockAsync(variantId));
        Assert.Equal(0, (await customer.GetJsonAsync("/api/cart")).GetProperty("itemCount").GetInt32());
        Assert.Single((await customer.GetJsonAsync("/api/orders")).EnumerateArray());
    }

    [Fact]
    public async Task Declined_payment_creates_no_order_and_returns_the_stock()
    {
        var variantId = await VariantIdAsync("PESTO-010");
        var before = await StockAsync(variantId);
        var customer = await app.NewCustomerAsync();
        await customer.PostOkAsync("/api/cart/items", new { productVariantId = variantId, quantity = 1 });

        var response = await customer.PostAsJsonAsync("/api/checkout", Checkout("tok_test_declined"));

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Contains("declined", (await response.JsonAsync()).GetProperty("title").GetString());
        Assert.Equal(before, await StockAsync(variantId));
        Assert.Empty((await customer.GetJsonAsync("/api/orders")).EnumerateArray());
        Assert.Equal(1, (await customer.GetJsonAsync("/api/cart")).GetProperty("itemCount").GetInt32()); // cart kept for a retry
    }

    [Fact]
    public async Task Two_shoppers_cannot_both_buy_the_last_plushies()
    {
        // "Honey (limited)" is seeded with exactly 3 in stock; both shoppers want all 3.
        var variantId = await VariantIdAsync("MOLLY-003");
        var a = await app.NewCustomerAsync();
        var b = await app.NewCustomerAsync();
        await a.PostOkAsync("/api/cart/items", new { productVariantId = variantId, quantity = 3 });
        await b.PostOkAsync("/api/cart/items", new { productVariantId = variantId, quantity = 3 });

        var results = await Task.WhenAll(a.PostAsJsonAsync("/api/checkout", Checkout()), b.PostAsJsonAsync("/api/checkout", Checkout()));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(0, await StockAsync(variantId));
    }

    [Fact]
    public async Task Custom_design_is_priced_by_the_server_not_the_browser()
    {
        var customer = await app.NewCustomerAsync();
        var cart = await customer.PostOkAsync("/api/cart/items", new
        {
            quantity = 1,
            // priceCents is not part of the contract; a tampered client sending it must be ignored.
            customDesign = new { name = "Sir Chomps", design = Dino, size = "jumbo", embroideryText = "Chomps", priceCents = 1 },
        });
        // dino 44.00 × 1.75 = 77.00, + crown 8.00, + embroidery 7.00
        Assert.Equal(9200, cart.GetProperty("items")[0].GetProperty("unitPriceCents").GetInt32());

        var bad = await customer.PostAsJsonAsync("/api/cart/items", new
        {
            quantity = 1, customDesign = new { name = "Nope", design = new { species = "dragon" }, size = "classic" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Coupon_rules_are_enforced()
    {
        var customer = await app.NewCustomerAsync();
        await customer.PostOkAsync("/api/cart/items", new { productVariantId = await VariantIdAsync("REX-012"), quantity = 1 }); // 44.00

        var tooSmall = await customer.PostAsJsonAsync("/api/cart/coupon", new { code = "FREEHUG" }); // needs 50.00
        Assert.Equal(HttpStatusCode.BadRequest, tooSmall.StatusCode);

        var cart = await customer.PostOkAsync("/api/cart/coupon", new { code = "welcome10" });
        Assert.Equal(440, cart.GetProperty("discountCents").GetInt32());

        var order = await (await customer.PostAsJsonAsync("/api/checkout", Checkout())).JsonAsync();
        Assert.Equal(440, order.GetProperty("discountCents").GetInt32());
        Assert.Equal("WELCOME10", order.GetProperty("couponCode").GetString());
    }

    [Fact]
    public async Task Customers_cannot_see_each_others_orders_or_the_admin_api()
    {
        var owner = await app.NewCustomerAsync();
        await owner.PostOkAsync("/api/cart/items", new { productVariantId = await VariantIdAsync("NOODLE-015"), quantity = 1 });
        var order = await (await owner.PostAsJsonAsync("/api/checkout", Checkout())).JsonAsync();
        var number = order.GetProperty("orderNumber").GetString();

        var stranger = await app.NewCustomerAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/orders/{number}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync("/api/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Anonymous().GetAsync("/api/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Anonymous().GetAsync($"/api/orders/{number}")).StatusCode);

        var admin = await app.AdminAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/orders")).StatusCode); // admin tokens aren't customer tokens
    }

    [Fact]
    public async Task Shipping_and_tax_depend_on_the_destination()
    {
        var customer = await app.NewCustomerAsync();
        await customer.PostOkAsync("/api/cart/items", new { productVariantId = await VariantIdAsync("PICKLE-019"), quantity = 1 }); // 48.00

        var us = await customer.GetJsonAsync("/api/checkout/shipping-methods?country=US");
        Assert.Equal("U.S. Tracked", us[0].GetProperty("name").GetString());
        var usTotals = await customer.GetJsonAsync($"/api/checkout/totals?country=US&province=NY&shippingMethodId={us[0].GetProperty("id").GetInt32()}");
        Assert.Equal(0, usTotals.GetProperty("taxCents").GetInt32());
        Assert.Equal(4800 + 1250, usTotals.GetProperty("totalCents").GetInt32());

        var de = await customer.GetJsonAsync("/api/checkout/shipping-methods?country=DE");
        Assert.Equal("International Tracked", de[0].GetProperty("name").GetString());

        // A Canadian-only method can't be used for a US address.
        var wrong = await customer.PostAsJsonAsync("/api/checkout", Checkout(shippingMethodId: 1, country: "US", province: "NY"));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_and_edit_a_product_with_variants()
    {
        var admin = await app.AdminAsync();
        var typeId = await app.DbAsync(db => db.ProductVariantTypes.Where(t => t.Type == "color").Select(t => t.ProductVariantTypeId).FirstAsync());
        var created = await admin.PostOkAsync("/api/admin/products", new
        {
            name = "Waffle the Bear", currency = "cad", active = true, tags = new[] { "New" },
            variants = new[]
            {
                new { id = 0, productVariantTypeId = typeId, value = "Syrup", sku = "waffle-1", priceCents = 4000, stock = 5, weightGrams = 300 },
                new { id = 0, productVariantTypeId = typeId, value = "Butter", sku = "waffle-2", priceCents = 4500, stock = 2, weightGrams = 300 },
            },
        });
        Assert.Equal("waffle-the-bear", created.GetProperty("slug").GetString());
        var id = created.GetProperty("id").GetInt32();
        var keep = created.GetProperty("variants")[0];

        // Dropping the second variant and repricing the first.
        var update = await admin.PutAsJsonAsync($"/api/admin/products/{id}", new
        {
            name = "Waffle the Bear", currency = "cad", active = true, tags = Array.Empty<string>(),
            variants = new[] { new { id = keep.GetProperty("id").GetInt32(), productVariantTypeId = typeId, value = "Syrup", sku = "WAFFLE-1", priceCents = 4100, stock = 5, weightGrams = 300 } },
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var storefront = (await app.Anonymous().GetJsonAsync("/api/products/waffle-the-bear")).GetProperty("summary");
        Assert.Single(storefront.GetProperty("variants").EnumerateArray());
        Assert.Equal(4100, storefront.GetProperty("fromPriceCents").GetInt32());

        var duplicateSku = await admin.PostAsJsonAsync("/api/admin/products", new
        {
            name = "Copycat", currency = "cad", active = true,
            variants = new[] { new { id = 0, productVariantTypeId = typeId, value = "Syrup", sku = "WAFFLE-1", priceCents = 100, stock = 1, weightGrams = 300 } },
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateSku.StatusCode);

        var noVariants = await admin.PostAsJsonAsync("/api/admin/products", new { name = "Empty", currency = "cad", variants = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, noVariants.StatusCode);
    }

    [Fact]
    public async Task Cancelling_an_unshipped_order_puts_the_stock_back()
    {
        var variantId = await VariantIdAsync("MOCHI-007");
        var before = await StockAsync(variantId);
        var customer = await app.NewCustomerAsync();
        await customer.PostOkAsync("/api/cart/items", new { productVariantId = variantId, quantity = 2 });
        var order = await (await customer.PostAsJsonAsync("/api/checkout", Checkout())).JsonAsync();
        Assert.Equal(before - 2, await StockAsync(variantId));

        var admin = await app.AdminAsync();
        var cancelled = await admin.PatchAsJsonAsync($"/api/admin/orders/{order.GetProperty("id").GetInt32()}/status", new { status = "cancelled" });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal(before, await StockAsync(variantId));

        // A cancelled order is final.
        var revive = await admin.PatchAsJsonAsync($"/api/admin/orders/{order.GetProperty("id").GetInt32()}/status", new { status = "paid" });
        Assert.Equal(HttpStatusCode.Conflict, revive.StatusCode);
        Assert.Equal(OrderStatus.Cancelled, await app.DbAsync(db => db.Orders.Where(o => o.OrderId == order.GetProperty("id").GetInt32()).Select(o => o.Status).SingleAsync()));
    }
}
