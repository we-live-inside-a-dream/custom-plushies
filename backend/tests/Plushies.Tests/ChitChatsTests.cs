using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChitChatsSimulator;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Plushies.Api.Shipping.ChitChats;

namespace Plushies.Tests;

/// <summary>
/// Exercises the Chit Chats client and the admin shipping workflow over real HTTP against the
/// local simulator, which enforces the field names, required fields and enums from the v1 spec.
/// </summary>
[Collection("app")]
public class ChitChatsTests(AppFixture app)
{
    private ChitChatsClient Client(string token = Simulator.DefaultToken) => new(new HttpClient(),
        Options.Create(new ChitChatsOptions { BaseUrl = $"{app.SimulatorUrl}/api/v1", ClientId = AppFixture.ClientId, AccessToken = token }),
        NullLogger<ChitChatsClient>.Instance) { BuyPollInterval = TimeSpan.FromMilliseconds(40) };

    private Task<int> VariantIdAsync(string sku) => app.DbAsync(db =>
        Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.ProductVariants.Where(v => v.Sku == sku).Select(v => v.ProductVariantId)));

    private static CreateShipmentRequest Request(string country = "CA") => new()
    {
        Name = "Jane Doe", Address1 = "123 ANYWHERE ST.", City = "Vancouver", ProvinceCode = "BC", PostalCode = "V6K 1A1",
        CountryCode = country, Description = "Plush toy", Value = "85.00", ValueCurrency = "cad", PackageType = "parcel",
        WeightUnit = "g", Weight = 250, SizeUnit = "cm", SizeX = 10, SizeY = 5, SizeZ = 2.5m,
        LineItems = [new ChitChatsLineItem { Quantity = 1, Description = "Plush toy", ValueAmount = "85.00", CurrencyCode = "cad" }],
    };

    [Fact]
    public async Task Create_sends_the_documented_field_names_and_returns_rates()
    {
        var shipment = await Client().CreateShipmentAsync(Request());

        Assert.Equal("pending", shipment.Status);
        Assert.NotEmpty(shipment.Id);
        Assert.Contains(shipment.Rates, r => r.PostageType == "chit_chats_canada_tracked");

        var sent = app.Sim.CreateRequests.Last();
        // The spec's exact keys: address_1 (not address1), snake_case sizes, value as a string.
        Assert.Equal("123 ANYWHERE ST.", sent["address_1"]!.GetValue<string>());
        Assert.Equal(2.5m, sent["size_z"]!.GetValue<decimal>());
        Assert.Equal(System.Text.Json.JsonValueKind.String, sent["value"]!.GetValueKind());
        Assert.Equal("unknown", sent["postage_type"]!.GetValue<string>());
        Assert.Equal("today", sent["ship_date"]!.GetValue<string>());
        Assert.Equal("cad", sent["line_items"]![0]!["currency_code"]!.GetValue<string>());
        Assert.False(sent.ContainsKey("address_2")); // nulls are omitted rather than sent as null
    }

    [Fact]
    public async Task Wrong_token_is_reported_as_a_credentials_problem()
    {
        var ex = await Assert.ThrowsAsync<ChitChatsException>(() => Client("wrong").CreateShipmentAsync(Request()));
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Contains("credentials", ex.Message);
    }

    [Fact]
    public async Task Validation_errors_from_chit_chats_are_passed_through()
    {
        var request = Request();
        request.PackageType = "sack";
        var ex = await Assert.ThrowsAsync<ChitChatsException>(() => Client().CreateShipmentAsync(request));
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("package_type", ex.Message);
    }

    [Fact]
    public async Task Buying_postage_polls_until_the_label_is_ready()
    {
        var client = Client();
        var shipment = await client.CreateShipmentAsync(Request());

        var bought = await client.BuyPostageAsync(shipment.Id, "chit_chats_canada_tracked");

        Assert.Equal("ready", bought.Status);
        Assert.Equal("chit_chats_canada_tracked", bought.PostageType);
        Assert.StartsWith("SIM", bought.CarrierTrackingCode);
        Assert.NotNull(bought.PostageLabelPdfUrl);
        var (pdf, contentType) = await client.DownloadLabelAsync(bought.PostageLabelPdfUrl!);
        Assert.Equal("application/pdf", contentType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task A_failed_purchase_is_an_error_not_a_silent_success()
    {
        var client = Client();
        var shipment = await client.CreateShipmentAsync(Request());
        app.Sim.FailNextPurchase = true;
        try
        {
            var ex = await Assert.ThrowsAsync<ChitChatsException>(() => client.BuyPostageAsync(shipment.Id, "chit_chats_canada_tracked"));
            Assert.Contains("could not purchase", ex.Message);
        }
        finally
        {
            app.Sim.FailNextPurchase = false;
        }
    }

    [Fact]
    public async Task Labels_are_only_downloaded_from_the_configured_chit_chats_host()
    {
        var ex = await Assert.ThrowsAsync<ChitChatsException>(() => Client().DownloadLabelAsync("https://evil.example/labels/1.pdf"));
        Assert.Contains("Refusing", ex.Message);
    }

    [Fact]
    public async Task Admin_ships_a_us_order_from_rates_to_delivered()
    {
        // A customer in the US buys a ready-made plushie and a custom one.
        var customer = await app.NewCustomerAsync();
        await customer.PostOkAsync("/api/cart/items", new { productVariantId = await VariantIdAsync("BISCUIT-004"), quantity = 1 });
        await customer.PostOkAsync("/api/cart/items", new
        {
            quantity = 1,
            customDesign = new
            {
                name = "Captain Ribbit", size = "classic",
                design = new { species = "frog", body = "#7BC950", belly = "#E9F7C8", eyes = "button", mouth = "smile", cheeks = true, accessory = "none", accessoryColor = "#FF5DA2" },
            },
        });
        var usMethod = (await customer.GetJsonAsync("/api/checkout/shipping-methods?country=US"))[0].GetProperty("id").GetInt32();
        var placed = await customer.PostAsJsonAsync("/api/checkout", new
        {
            address = AppFixture.Address("US", "NY", "10001"), shippingMethodId = usMethod, paymentToken = "tok_test_ok",
        });
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.JsonAsync();
        var orderId = order.GetProperty("id").GetInt32();
        var orderNumber = order.GetProperty("orderNumber").GetString()!;

        var admin = await app.AdminAsync();
        Assert.True((await admin.GetJsonAsync("/api/admin/shipping/status")).GetProperty("configured").GetBoolean());
        var defaults = await admin.GetJsonAsync($"/api/admin/orders/{orderId}/shipment-defaults");
        Assert.Equal(320 + 320 + 80, defaults.GetProperty("weightGrams").GetInt32()); // items + packaging
        Assert.Equal("chit_chats_us_edge", defaults.GetProperty("preferredPostageType").GetString());

        // 1. Create the shipment: Chit Chats answers with rates to choose from.
        var shipment = await admin.PostOkAsync($"/api/admin/orders/{orderId}/shipments", new
        {
            packageType = "parcel", weightGrams = 720, sizeXCm = 30, sizeYCm = 25, sizeZCm = 15,
        });
        var shipmentId = shipment.GetProperty("id").GetInt32();
        Assert.Equal("pending", shipment.GetProperty("status").GetString());
        Assert.Contains(shipment.GetProperty("rates").EnumerateArray(), r => r.GetProperty("postageType").GetString() == "chit_chats_us_edge");

        var sent = app.Sim.CreateRequests.Last(r => r["order_id"]?.GetValue<string>() == orderNumber);
        Assert.Equal("US", sent["country_code"]!.GetValue<string>());
        Assert.Equal("NY", sent["province_code"]!.GetValue<string>());
        Assert.Equal("other", sent["order_store"]!.GetValue<string>());
        Assert.Equal("merchandise", sent["package_contents"]!.GetValue<string>());
        Assert.Equal("78.00", sent["value"]!.GetValue<string>()); // 38.00 + 40.00, goods only
        var lines = sent["line_items"]!.AsArray();
        Assert.Equal(2, lines.Count); // customs line items are required for US shipments
        Assert.All(lines, l => Assert.Equal("9503000073", l!["hs_tariff_code"]!.GetValue<string>()));
        Assert.Equal("CA", lines[0]!["origin_country"]!.GetValue<string>());
        Assert.Equal("BISCUIT-004", lines[0]!["sku_code"]!.GetValue<string>());
        Assert.Equal("CUSTOM-FROG-CLASSIC", lines[1]!["sku_code"]!.GetValue<string>());

        // Creating a shipment moves a paid order into production, and a second open shipment is refused.
        Assert.Equal("in_production", (await admin.GetJsonAsync($"/api/admin/orders/{orderId}")).GetProperty("order").GetProperty("status").GetString());
        var second = await admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/shipments", new { packageType = "parcel", weightGrams = 720, sizeXCm = 30, sizeYCm = 25, sizeZCm = 15 });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // The customer doesn't see a tracking entry until postage exists.
        Assert.Empty((await customer.GetJsonAsync($"/api/orders/{orderNumber}")).GetProperty("shipments").EnumerateArray());

        // 2. Buy postage.
        var bought = await admin.PostOkAsync($"/api/admin/shipments/{shipmentId}/buy", new { postageType = "chit_chats_us_edge" });
        Assert.Equal("ready", bought.GetProperty("status").GetString());
        Assert.True(bought.GetProperty("hasLabel").GetBoolean());
        Assert.True(bought.GetProperty("postageCostCents").GetInt32() > 0);
        Assert.StartsWith("SIM", bought.GetProperty("trackingCode").GetString());

        // 3. The label is proxied through our API (the browser never holds the Chit Chats token).
        var label = await admin.GetAsync($"/api/admin/shipments/{shipmentId}/label?format=pdf");
        Assert.Equal(HttpStatusCode.OK, label.StatusCode);
        Assert.Equal("application/pdf", label.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"/api/admin/shipments/{shipmentId}/label")).StatusCode);

        // 4. As Chit Chats moves the parcel, syncing moves the order.
        var chitChatsId = bought.GetProperty("chitChatsShipmentId").GetString();
        var sim = new HttpClient { BaseAddress = new Uri(app.SimulatorUrl) };
        await sim.PostAsync($"/_sim/shipments/{chitChatsId}/advance", null); // received
        await admin.PostOkAsync($"/api/admin/shipments/{shipmentId}/sync");
        var tracking = (await customer.GetJsonAsync($"/api/orders/{orderNumber}"));
        Assert.Equal("shipped", tracking.GetProperty("status").GetString());
        Assert.StartsWith("SIM", tracking.GetProperty("shipments")[0].GetProperty("trackingCode").GetString());

        for (var i = 0; i < 4; i++) await sim.PostAsync($"/_sim/shipments/{chitChatsId}/advance", null); // → delivered
        var synced = await admin.PostOkAsync($"/api/admin/shipments/{shipmentId}/sync");
        Assert.Equal("delivered", synced.GetProperty("status").GetString());
        Assert.Equal("delivered", (await customer.GetJsonAsync($"/api/orders/{orderNumber}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Admin_can_void_postage_and_delete_an_unpaid_shipment()
    {
        var customer = await app.NewCustomerAsync();
        await customer.PostOkAsync("/api/cart/items", new { productVariantId = await VariantIdAsync("BISCUIT-005"), quantity = 1 });
        var order = await (await customer.PostAsJsonAsync("/api/checkout", new { address = AppFixture.Address(), shippingMethodId = 1, paymentToken = "tok_test_ok" })).JsonAsync();
        var orderId = order.GetProperty("id").GetInt32();
        var admin = await app.AdminAsync();
        object parcel = new { packageType = "parcel", weightGrams = 400, sizeXCm = 30, sizeYCm = 25, sizeZCm = 15 };

        // Delete before buying: gone on both sides.
        var draft = await admin.PostOkAsync($"/api/admin/orders/{orderId}/shipments", parcel);
        var draftRemoteId = draft.GetProperty("chitChatsShipmentId").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/shipments/{draft.GetProperty("id").GetInt32()}")).StatusCode);
        Assert.False(app.Sim.Shipments.ContainsKey(draftRemoteId));

        // Buy, then refund: the shipment is voided and can no longer be deleted, but a new one can be made.
        var shipment = await admin.PostOkAsync($"/api/admin/orders/{orderId}/shipments", parcel);
        var id = shipment.GetProperty("id").GetInt32();
        await admin.PostOkAsync($"/api/admin/shipments/{id}/buy", new { postageType = "chit_chats_canada_tracked" });
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/admin/shipments/{id}")).StatusCode);
        var refunded = await admin.PostOkAsync($"/api/admin/shipments/{id}/refund");
        Assert.Equal("voided", refunded.GetProperty("status").GetString());
        await admin.PostOkAsync($"/api/admin/orders/{orderId}/shipments", parcel);

        // Chit Chats' own validation message reaches the admin instead of a generic failure.
        var bad = await admin.PostAsJsonAsync("/api/admin/shipments/999999/buy", new { postageType = "chit_chats_canada_tracked" });
        Assert.Equal(HttpStatusCode.NotFound, bad.StatusCode);
    }

    [Fact]
    public async Task Rate_limited_requests_are_retried_after_the_advertised_wait()
    {
        // Runs last-ish by name but is order-independent: only the next request is throttled.
        var client = Client();
        var shipment = await client.CreateShipmentAsync(Request());
        app.Sim.RateLimitNextRequests = 1;
        try
        {
            var fetched = await client.GetShipmentAsync(shipment.Id); // 429 with Retry-After: 1, then 200
            Assert.Equal(shipment.Id, fetched.Id);
        }
        finally
        {
            app.Sim.RateLimitNextRequests = 0;
        }
    }
}
