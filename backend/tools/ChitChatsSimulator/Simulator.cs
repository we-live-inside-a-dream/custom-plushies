using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace ChitChatsSimulator;

/// <summary>
/// A small local stand-in for the Chit Chats API v1, for development and automated tests.
/// It follows the request/response shapes and validation rules of https://chitchats.com/docs/api/v1
/// but its rates, tracking numbers and labels are invented. It is NOT a substitute for testing
/// against the real sandbox at https://staging.chitchats.com before going live.
/// </summary>
public static class Simulator
{
    public const string DefaultToken = "sim-token";

    private static readonly string[] Required =
        ["name", "address_1", "city", "country_code", "value", "value_currency", "package_type", "weight_unit", "weight", "size_unit", "size_x", "size_y", "size_z"];
    private static readonly string[] PackageTypes =
    [
        "card", "letter", "envelope", "thick_envelope", "parcel", "flat_rate_envelope", "flat_rate_legal_envelope",
        "flat_rate_padded_envelope", "flat_rate_gift_card_envelope", "flat_rate_window_envelope", "flat_rate_cardboard_envelope",
        "small_flat_rate_envelope", "small_flat_rate_box", "medium_flat_rate_box_1", "medium_flat_rate_box_2", "large_flat_rate_box",
    ];
    private static readonly string[] OrderStores =
        ["adobe_commerce", "amazon", "bigcommerce", "ebay", "etsy", "shipstation", "shopify", "squarespace", "woocommerce", "other"];
    private static readonly string[] Progress = ["ready", "received", "released", "inducted", "in_transit", "delivered"];

    public class State
    {
        public ConcurrentDictionary<string, JsonObject> Shipments { get; } = new();
        public ConcurrentDictionary<string, DateTime> PurchaseRequestedAt { get; } = new();
        /// <summary>Every create-shipment body received, newest last. Lets tests assert on what was sent.</summary>
        public ConcurrentQueue<JsonObject> CreateRequests { get; } = new();
        public bool FailNextPurchase { get; set; }
        public int RateLimitNextRequests { get; set; }
        public TimeSpan PurchaseDelay { get; set; } = TimeSpan.FromMilliseconds(300);
    }

    public static WebApplication Build(string[] args, string token = DefaultToken)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        var state = new State();
        builder.Services.AddSingleton(state);
        var app = builder.Build();

        static IResult Error(int status, string message) =>
            Results.Json(new JsonObject { ["error"] = new JsonObject { ["message"] = message } }, statusCode: status);

        // Everything except the simulator's own control endpoints needs the access token, sent bare.
        app.Use(async (ctx, next) =>
        {
            if (!ctx.Request.Path.StartsWithSegments("/_sim"))
            {
                if (state.RateLimitNextRequests > 0)
                {
                    state.RateLimitNextRequests--;
                    ctx.Response.Headers.RetryAfter = "1";
                    await Error(429, "Too Many Requests").ExecuteAsync(ctx);
                    return;
                }
                if (ctx.Request.Headers.Authorization.ToString() != token)
                {
                    await Error(401, "Unauthorized").ExecuteAsync(ctx);
                    return;
                }
            }
            await next();
        });

        var api = app.MapGroup("/api/v1/clients/{clientId}");

        api.MapPost("shipments", async (string clientId, HttpRequest request) =>
        {
            JsonObject body;
            try { body = (await JsonNode.ParseAsync(request.Body))!.AsObject(); }
            catch { return Error(400, "Request body must be a JSON object"); }
            state.CreateRequests.Enqueue((JsonObject)body.DeepClone());
            if (Validate(body) is { } problem) return Error(400, problem);

            var id = NewId();
            var country = Text(body, "country_code")!.ToUpperInvariant();
            var postageType = Text(body, "postage_type") is { Length: > 0 } p ? p : "unknown";
            var shipment = new JsonObject
            {
                ["id"] = id, ["status"] = "pending", ["batch_id"] = null,
                ["to_name"] = Text(body, "name"), ["to_address_1"] = Text(body, "address_1"), ["to_address_2"] = Text(body, "address_2"),
                ["to_city"] = Text(body, "city"), ["to_province_code"] = Text(body, "province_code"), ["to_postal_code"] = Text(body, "postal_code"),
                ["to_country_code"] = country, ["to_phone"] = Text(body, "phone"), ["to_email"] = Text(body, "email"),
                ["package_contents"] = Text(body, "package_contents") ?? "merchandise", ["description"] = Text(body, "description") ?? "",
                ["value"] = Text(body, "value"), ["value_currency"] = Text(body, "value_currency"), ["order_id"] = Text(body, "order_id") ?? "",
                ["order_store"] = Text(body, "order_store"), ["package_type"] = Text(body, "package_type"),
                ["size_unit"] = Text(body, "size_unit"), ["size_x"] = Number(body, "size_x"), ["size_y"] = Number(body, "size_y"), ["size_z"] = Number(body, "size_z"),
                ["weight_unit"] = Text(body, "weight_unit"), ["weight"] = Number(body, "weight"),
                ["is_insured"] = false, ["is_insurance_requested"] = body["insurance_requested"]?.GetValue<bool>() ?? false,
                ["is_signature_requested"] = body["signature_requested"]?.GetValue<bool>() ?? false,
                ["postage_type"] = postageType, ["carrier"] = "", ["carrier_tracking_code"] = null, ["tracking_url"] = $"{Origin(request)}/tracking/{id}",
                ["ship_date"] = Text(body, "ship_date") is { } d && d != "today" ? d : DateTime.UtcNow.ToString("yyyy-MM-dd"),
                ["purchase_amount"] = "0.00", ["postage_fee"] = "0.00", ["insurance_fee"] = "0.00", ["federal_tax"] = "0.00", ["federal_tax_label"] = "GST",
                ["created_at"] = DateTime.UtcNow.ToString("o"),
                ["postage_label_png_url"] = null, ["postage_label_pdf_url"] = null, ["postage_label_zpl_url"] = null,
                ["line_items"] = body["line_items"]?.DeepClone() ?? new JsonArray(),
            };
            shipment["rates"] = Rates(shipment);
            if (Text(body, "cheapest_postage_type_requested") == "yes")
                shipment["postage_type"] = shipment["rates"]![0]!["postage_type"]!.GetValue<string>();
            state.Shipments[id] = shipment;
            return Results.Json(new JsonObject { ["shipment"] = shipment.DeepClone() }, statusCode: 201,
                contentType: "application/json").WithLocation($"{Origin(request)}/api/v1/clients/{clientId}/shipments/{id}");
        });

        api.MapGet("shipments/{id}", (string id, HttpRequest request) =>
        {
            if (!state.Shipments.TryGetValue(id, out var s)) return Error(404, "Not Found");
            Settle(state, s, id, Origin(request));
            return Results.Json(new JsonObject { ["shipment"] = s.DeepClone() });
        });

        api.MapPatch("shipments/{id}/refresh", async (string id, HttpRequest request) =>
        {
            if (!state.Shipments.TryGetValue(id, out var s)) return Error(404, "Not Found");
            var body = (await JsonNode.ParseAsync(request.Body))?.AsObject() ?? [];
            foreach (var key in new[] { "package_type", "weight_unit", "weight", "size_unit", "size_x", "size_y", "size_z", "postage_type", "ship_date" })
                if (body[key] is { } v) s[key] = v.DeepClone();
            if (body["line_items"] is { } items) s["line_items"] = items.DeepClone();
            s["rates"] = Rates(s);
            return Results.Json(new JsonObject { ["shipment"] = s.DeepClone() });
        });

        api.MapPatch("shipments/{id}/buy", async (string id, HttpRequest request) =>
        {
            if (!state.Shipments.TryGetValue(id, out var s)) return Error(404, "Not Found");
            var body = (await JsonNode.ParseAsync(request.Body))?.AsObject() ?? [];
            var postageType = Text(body, "postage_type") ?? Text(s, "postage_type");
            if (Text(s, "status") is not ("pending" or "postage_purchase_failed")) return Error(400, "Shipment postage has already been purchased");
            if (s["rates"]!.AsArray().All(r => r!["postage_type"]!.GetValue<string>() != postageType))
                return Error(400, $"Postage type {postageType} is not available for this shipment");
            s["postage_type"] = postageType;
            s["status"] = "postage_requested";
            state.PurchaseRequestedAt[id] = DateTime.UtcNow;
            return Results.Ok();
        });

        api.MapPatch("shipments/{id}/refund", (string id) =>
        {
            if (!state.Shipments.TryGetValue(id, out var s)) return Error(404, "Not Found");
            if (Text(s, "status") is "pending" or "postage_requested" or "postage_purchase_failed") return Error(400, "Shipment has no postage to refund");
            if (Text(s, "status") is "voided") return Error(400, "Shipment has already been refunded");
            s["status"] = "voided";
            return Results.Ok();
        });

        api.MapDelete("shipments/{id}", (string id) =>
        {
            if (!state.Shipments.TryGetValue(id, out var s)) return Error(404, "Not Found");
            if (Text(s, "status") is not ("pending" or "postage_purchase_failed"))
                return Error(400, "Shipment cannot be deleted once postage has been purchased");
            state.Shipments.TryRemove(id, out _);
            return Results.NoContent();
        });

        app.MapGet("/labels/shipments/{file}", (string file) =>
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (!state.Shipments.ContainsKey(id)) return Error(404, "Not Found");
            return Path.GetExtension(file) switch
            {
                ".pdf" => Results.Bytes(SamplePdf(id), "application/pdf"),
                ".zpl" => Results.Text($"^XA^FO50,50^ADN,36,20^FDSIMULATED {id}^FS^XZ", "text/plain"),
                _ => Results.Bytes(Convert.FromBase64String(OnePixelPng), "image/png"),
            };
        });

        // ---- Controls for demos and tests; not part of the Chit Chats API ----
        app.MapPost("/_sim/shipments/{id}/advance", (string id) =>
        {
            if (!state.Shipments.TryGetValue(id, out var s)) return Error(404, "Not Found");
            var index = Array.IndexOf(Progress, Text(s, "status"));
            if (index < 0 || index == Progress.Length - 1) return Error(400, $"Can't advance a shipment that is {Text(s, "status")}");
            s["status"] = Progress[index + 1];
            return Results.Json(new JsonObject { ["status"] = Progress[index + 1] });
        });
        app.MapPost("/_sim/fail-next-purchase", () => { state.FailNextPurchase = true; return Results.Ok(); });
        app.MapPost("/_sim/rate-limit/{count:int}", (int count) => { state.RateLimitNextRequests = count; return Results.Ok(); });
        app.MapGet("/", () => "Chit Chats simulator is running. See backend/tools/ChitChatsSimulator.");

        return app;
    }

    private static string? Validate(JsonObject b)
    {
        foreach (var field in Required)
            if (b[field] is null || (b[field]!.GetValueKind() == System.Text.Json.JsonValueKind.String && Text(b, field) == ""))
                return $"{field} is required";
        if (!PackageTypes.Contains(Text(b, "package_type"))) return "package_type is not included in the list";
        if (Text(b, "weight_unit") is not ("g" or "kg" or "oz" or "lb")) return "weight_unit is not included in the list";
        if (Text(b, "size_unit") is not ("cm" or "m" or "in")) return "size_unit is not included in the list";
        if (Text(b, "value_currency") is not ("usd" or "cad")) return "value_currency is not included in the list";
        if (Text(b, "package_contents") is { } contents && contents is not ("merchandise" or "documents" or "gift" or "returned_goods" or "sample" or "other"))
            return "package_contents is not included in the list";
        if (Text(b, "order_store") is { Length: > 0 } store && !OrderStores.Contains(store)) return "order_store is not included in the list";
        if (Text(b, "country_code") is not { Length: 2 }) return "country_code must be a 2 letter code";
        if (!decimal.TryParse(Text(b, "value"), NumberStyles.Number, CultureInfo.InvariantCulture, out _)) return "value is not a number";
        if (Number(b, "weight") <= 0) return "weight must be greater than 0";
        if (Text(b, "cheapest_postage_type_requested") is { } cheapest)
        {
            if (cheapest != "yes") return "cheapest_postage_type_requested is not included in the list";
            if ((Text(b, "postage_type") ?? "unknown") != "unknown") return "postage_type must be unknown when cheapest_postage_type_requested is yes";
        }
        if (Text(b, "ship_date") is { } date && date != "today" && !DateOnly.TryParseExact(date, "yyyy-MM-dd", out _))
            return "ship_date must be today or YYYY-MM-DD";

        var items = b["line_items"] as JsonArray;
        var international = !string.Equals(Text(b, "country_code"), "CA", StringComparison.OrdinalIgnoreCase);
        if (international && (items is null || items.Count == 0)) return "line_items are required for US and International shipments";
        if (items is null && string.IsNullOrEmpty(Text(b, "description"))) return "description is required if line_items are not provided";
        foreach (var item in items ?? [])
        {
            var o = item!.AsObject();
            foreach (var field in new[] { "quantity", "description", "value_amount", "currency_code" })
                if (o[field] is null) return $"line_items {field} is required";
            if (Text(o, "currency_code") is not ("usd" or "cad")) return "line_items currency_code is not included in the list";
        }
        return null;
    }

    /// <summary>Invented rates, shaped like the real ones, cheapest first.</summary>
    private static JsonArray Rates(JsonObject s)
    {
        var grams = Number(s, "weight") * (Text(s, "weight_unit") switch { "kg" => 1000m, "oz" => 28.35m, "lb" => 453.6m, _ => 1m });
        (string Type, string Carrier, string Name, string Days, decimal Base)[] options = Text(s, "to_country_code") switch
        {
            "CA" => [("chit_chats_slim", "chit_chats", "Chit Chats Slim", "2-9 business days", 5.25m),
                     ("chit_chats_canada_tracked", "chit_chats", "Chit Chats Canada Tracked", "2-8 business days", 8.40m),
                     ("chit_chats_select", "chit_chats", "Chit Chats Select", "1-5 business days", 12.10m)],
            "US" => [("chit_chats_us_edge", "chit_chats", "Chit Chats U.S. Edge", "3-10 business days", 6.90m),
                     ("usps_ground_advantage", "usps", "USPS Ground Advantage", "2-5 business days", 8.75m),
                     ("chit_chats_us_select", "chit_chats", "Chit Chats U.S. Select", "2-6 business days", 11.30m)],
            _ => [("chit_chats_international_tracked", "chit_chats", "Chit Chats International Tracked", "6-15 business days", 16.80m),
                  ("usps_first_class_package_international_service", "usps", "USPS First-Class Package International Service", "7-21 business days", 21.50m)],
        };
        var rates = new JsonArray();
        foreach (var o in options)
        {
            var fee = Math.Round(o.Base + grams / 1000m * 2.5m, 2);
            var tax = Text(s, "to_country_code") == "CA" ? Math.Round(fee * 0.05m, 2) : 0m;
            rates.Add(new JsonObject
            {
                ["postage_type"] = o.Type, ["postage_carrier_type"] = o.Carrier, ["postage_description"] = o.Name,
                ["signature_confirmation_description"] = null, ["delivery_duties_paid_description"] = null,
                ["delivery_time_description"] = o.Days, ["tracking_type_description"] = "Full tracking", ["is_insured"] = false,
                ["purchase_amount"] = Money(fee + tax), ["provincial_tax"] = null, ["provincial_tax_label"] = null,
                ["federal_tax"] = Money(tax), ["federal_tax_label"] = "GST", ["postage_fee"] = Money(fee), ["insurance_fee"] = "0.00",
                ["delivery_fee"] = null, ["tariff_fee"] = null, ["payment_amount"] = Money(fee + tax),
            });
        }
        return rates;
    }

    /// <summary>Moves a postage_requested shipment to ready (or failed) once the simulated purchase delay has passed.</summary>
    private static void Settle(State state, JsonObject s, string id, string origin)
    {
        if (Text(s, "status") != "postage_requested") return;
        if (!state.PurchaseRequestedAt.TryGetValue(id, out var at) || DateTime.UtcNow - at < state.PurchaseDelay) return;
        if (state.FailNextPurchase)
        {
            state.FailNextPurchase = false;
            s["status"] = "postage_purchase_failed";
            return;
        }
        var rate = s["rates"]!.AsArray().First(r => r!["postage_type"]!.GetValue<string>() == Text(s, "postage_type"))!;
        s["status"] = "ready";
        s["carrier"] = rate["postage_carrier_type"]!.GetValue<string>();
        s["carrier_tracking_code"] = "SIM" + Random.Shared.NextInt64(1_000_000_000, 9_999_999_999);
        s["purchase_amount"] = rate["purchase_amount"]!.GetValue<string>();
        s["postage_fee"] = rate["postage_fee"]!.GetValue<string>();
        s["federal_tax"] = rate["federal_tax"]!.GetValue<string>();
        s["postage_label_png_url"] = $"{origin}/labels/shipments/{id}.png";
        s["postage_label_pdf_url"] = $"{origin}/labels/shipments/{id}.pdf";
        s["postage_label_zpl_url"] = $"{origin}/labels/shipments/{id}.zpl";
        s["rates"] = new JsonArray();
    }

    private static string Origin(HttpRequest r) => $"{r.Scheme}://{r.Host}";
    private static string? Text(JsonObject o, string key) =>
        o[key] is { } n && n.GetValueKind() == System.Text.Json.JsonValueKind.String ? n.GetValue<string>() : null;
    private static decimal Number(JsonObject o, string key) => o[key]?.GetValueKind() switch
    {
        System.Text.Json.JsonValueKind.Number => o[key]!.GetValue<decimal>(),
        System.Text.Json.JsonValueKind.String => decimal.TryParse(o[key]!.GetValue<string>(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0,
        _ => 0,
    };
    private static string Money(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);
    private static string NewId() => string.Concat(Enumerable.Range(0, 10).Select(_ => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[Random.Shared.Next(32)]));

    private const string OnePixelPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private static byte[] SamplePdf(string id)
    {
        var text = $"BT /F1 18 Tf 40 200 Td (SIMULATED LABEL - NOT VALID POSTAGE) Tj 0 -30 Td (Shipment {id}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 432 288] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {text.Length} >>\nstream\n{text}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(sb.Length);
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = sb.Length;
        sb.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}

internal static class ResultExtensions
{
    public static IResult WithLocation(this IResult inner, string location) => new LocationResult(inner, location);

    private sealed class LocationResult(IResult inner, string location) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.Location = location;
            return inner.ExecuteAsync(httpContext);
        }
    }
}
