using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plushies.Api.Shipping.ChitChats;

// Request/response shapes for the Chit Chats API v1 (https://chitchats.com/docs/api/v1).
// Property names are serialised as snake_case by ChitChatsJson.Options.

/// <summary>Body of POST /shipments. Required fields per the spec are non-nullable here.</summary>
public class CreateShipmentRequest
{
    public required string Name { get; set; }
    [JsonPropertyName("address_1")] public required string Address1 { get; set; }
    [JsonPropertyName("address_2")] public string? Address2 { get; set; }
    public required string City { get; set; }
    public string? ProvinceCode { get; set; }
    public string? PostalCode { get; set; }
    public required string CountryCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    /// <summary>merchandise | documents | gift | returned_goods | sample | other</summary>
    public string PackageContents { get; set; } = "merchandise";
    /// <summary>Customs description. Required when line_items are not provided.</summary>
    public string? Description { get; set; }
    /// <summary>Declared value as a decimal string, e.g. "84.99".</summary>
    public required string Value { get; set; }
    /// <summary>usd | cad</summary>
    public required string ValueCurrency { get; set; }
    public string? OrderId { get; set; }
    /// <summary>adobe_commerce | amazon | bigcommerce | ebay | etsy | shipstation | shopify | squarespace | woocommerce | other</summary>
    public string? OrderStore { get; set; }
    public required string PackageType { get; set; }
    /// <summary>g | kg | oz | lb</summary>
    public required string WeightUnit { get; set; }
    public required decimal Weight { get; set; }
    /// <summary>cm | m | in</summary>
    public required string SizeUnit { get; set; }
    public required decimal SizeX { get; set; }
    public required decimal SizeY { get; set; }
    public required decimal SizeZ { get; set; }
    public bool InsuranceRequested { get; set; }
    public bool SignatureRequested { get; set; }
    public string? VatReference { get; set; }
    public bool DutiesPaidRequested { get; set; }
    /// <summary>"unknown" creates the shipment and returns rates without choosing postage.</summary>
    public string PostageType { get; set; } = ChitChatsPostage.Unknown;
    /// <summary>Only "yes" is accepted, and only together with postage_type "unknown".</summary>
    public string? CheapestPostageTypeRequested { get; set; }
    /// <summary>"today" or YYYY-MM-DD: when Chit Chats is expected to receive the parcel.</summary>
    public string ShipDate { get; set; } = "today";
    public List<ChitChatsLineItem>? LineItems { get; set; }
}

/// <summary>Customs line item. Required for US and international shipments.</summary>
public class ChitChatsLineItem
{
    public required int Quantity { get; set; }
    public required string Description { get; set; }
    public required string ValueAmount { get; set; }
    public required string CurrencyCode { get; set; }
    public string? HsTariffCode { get; set; }
    /// <summary>Required for EU shipments at postage purchase (max 70 characters).</summary>
    public string? SkuCode { get; set; }
    public string? OriginCountry { get; set; }
    public decimal? Weight { get; set; }
    public string? WeightUnit { get; set; }
    /// <summary>Required for EU shipments at postage purchase.</summary>
    public string? ManufacturerInternalId { get; set; }
}

/// <summary>Body of PATCH /shipments/{id}/refresh: updates the parcel and re-quotes rates.</summary>
public class RefreshShipmentRequest
{
    public string? PackageType { get; set; }
    public string? WeightUnit { get; set; }
    public decimal? Weight { get; set; }
    public string? SizeUnit { get; set; }
    public decimal? SizeX { get; set; }
    public decimal? SizeY { get; set; }
    public decimal? SizeZ { get; set; }
    public bool? InsuranceRequested { get; set; }
    public bool? SignatureRequested { get; set; }
    public string? PostageType { get; set; }
    public string? ShipDate { get; set; }
}

public class BuyPostageRequest
{
    public required string PostageType { get; set; }
}

public class ShipmentEnvelope
{
    public ChitChatsShipment? Shipment { get; set; }
}

public class ChitChatsShipment
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    [JsonConverter(typeof(FlexibleStringConverter))] public string? BatchId { get; set; }
    public string? ToName { get; set; }
    public string? ToCountryCode { get; set; }
    public string? OrderId { get; set; }
    public string? PackageType { get; set; }
    public string? PostageType { get; set; }
    public string? Carrier { get; set; }
    public string? CarrierTrackingCode { get; set; }
    public string? TrackingUrl { get; set; }
    public string? ShipDate { get; set; }
    [JsonConverter(typeof(FlexibleStringConverter))] public string? PurchaseAmount { get; set; }
    [JsonConverter(typeof(FlexibleStringConverter))] public string? PostageFee { get; set; }
    public bool IsInsured { get; set; }
    public string? PostageLabelPngUrl { get; set; }
    public string? PostageLabelPdfUrl { get; set; }
    public string? PostageLabelZplUrl { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public List<ChitChatsRate> Rates { get; set; } = [];
}

public class ChitChatsRate
{
    public string PostageType { get; set; } = "";
    public string? PostageCarrierType { get; set; }
    public string? PostageDescription { get; set; }
    public string? SignatureConfirmationDescription { get; set; }
    public string? DeliveryDutiesPaidDescription { get; set; }
    public string? DeliveryTimeDescription { get; set; }
    public string? TrackingTypeDescription { get; set; }
    public bool IsInsured { get; set; }
    [JsonConverter(typeof(FlexibleStringConverter))] public string? PurchaseAmount { get; set; }
    [JsonConverter(typeof(FlexibleStringConverter))] public string? PostageFee { get; set; }
    [JsonConverter(typeof(FlexibleStringConverter))] public string? FederalTax { get; set; }
    [JsonConverter(typeof(FlexibleStringConverter))] public string? ProvincialTax { get; set; }
    [JsonConverter(typeof(FlexibleStringConverter))] public string? InsuranceFee { get; set; }
    [JsonConverter(typeof(FlexibleStringConverter))] public string? TariffFee { get; set; }
    /// <summary>What will actually be charged to the Chit Chats account, in CAD.</summary>
    [JsonConverter(typeof(FlexibleStringConverter))] public string? PaymentAmount { get; set; }
}

public class ChitChatsErrorEnvelope
{
    public ChitChatsError? Error { get; set; }
}

public class ChitChatsError
{
    public string? Message { get; set; }
}

public static class ChitChatsPostage
{
    public const string Unknown = "unknown";
}

/// <summary>Shipment statuses as documented by Chit Chats, plus the two transient purchase states.</summary>
public static class ChitChatsStatus
{
    public const string Pending = "pending";
    public const string PostageRequested = "postage_requested";
    public const string PostagePurchaseFailed = "postage_purchase_failed";
    public const string Ready = "ready";
    public const string Received = "received";
    public const string Released = "released";
    public const string Inducted = "inducted";
    public const string InTransit = "in_transit";
    public const string Delivered = "delivered";
    public const string Exception = "exception";
    public const string Voided = "voided";
    public const string Canceled = "canceled";
    public const string Resolved = "resolved";
}

public static class ChitChatsEnums
{
    public static readonly string[] PackageTypes =
    [
        "card", "letter", "envelope", "thick_envelope", "parcel", "flat_rate_envelope", "flat_rate_legal_envelope",
        "flat_rate_padded_envelope", "flat_rate_gift_card_envelope", "flat_rate_window_envelope",
        "flat_rate_cardboard_envelope", "small_flat_rate_envelope", "small_flat_rate_box", "medium_flat_rate_box_1",
        "medium_flat_rate_box_2", "large_flat_rate_box",
    ];

    public static readonly string[] Currencies = ["cad", "usd"];
}

public static class ChitChatsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}

/// <summary>
/// Money and id fields are documented as strings, but be liberal in what we accept:
/// a number in their place is read as its text instead of failing the whole response.
/// </summary>
public class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.Null => null,
        JsonTokenType.String => reader.GetString(),
        JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture),
        JsonTokenType.True => "true",
        JsonTokenType.False => "false",
        _ => throw new JsonException($"Unexpected {reader.TokenType} where a string was expected."),
    };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
