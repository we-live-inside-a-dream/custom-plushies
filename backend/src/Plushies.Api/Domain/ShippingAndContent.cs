namespace Plushies.Api.Domain;

/// <summary>
/// What the customer picks at checkout. Chit Chats only returns real rates once a shipment
/// exists, so the storefront charges from this table and the admin buys the actual postage later.
/// </summary>
public class ShippingMethod
{
    public int ShippingMethodId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    /// <summary>ISO country code, or "*" for everywhere not matched by a specific row.</summary>
    public required string CountryCode { get; set; }
    public int PriceCents { get; set; }
    public int? FreeOverCents { get; set; }
    public string? EstimatedDays { get; set; }
    /// <summary>Preferred Chit Chats postage_type, pre-selected when the admin buys postage.</summary>
    public string? ChitChatsPostageType { get; set; }
    public bool Active { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public class TaxRate
{
    public int TaxRateId { get; set; }
    public required string CountryCode { get; set; }
    /// <summary>Null means "the whole country".</summary>
    public string? ProvinceCode { get; set; }
    public required string Label { get; set; }
    /// <summary>Percent, e.g. 13 or 14.975.</summary>
    public decimal RatePercent { get; set; }
}

/// <summary>Local record of a Chit Chats shipment. Status values mirror the Chit Chats API.</summary>
public class Shipment
{
    public int ShipmentId { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public required string ChitChatsShipmentId { get; set; }
    public required string Status { get; set; }
    public string? PostageType { get; set; }
    public string? Carrier { get; set; }
    public string? CarrierTrackingCode { get; set; }
    public string? TrackingUrl { get; set; }
    public int? PostageCostCents { get; set; }
    public required string PackageType { get; set; }
    public int WeightGrams { get; set; }
    public decimal SizeXCm { get; set; }
    public decimal SizeYCm { get; set; }
    public decimal SizeZCm { get; set; }
    public string? LabelPngUrl { get; set; }
    public string? LabelPdfUrl { get; set; }
    public string? LabelZplUrl { get; set; }
    public string? BatchId { get; set; }
    public DateOnly? ShipDate { get; set; }
    /// <summary>The `rates` array from the last Chit Chats response, kept as raw JSON.</summary>
    public string? RatesJson { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Chit Chats statuses after which nothing more will change.</summary>
    public static readonly string[] TerminalStatuses = ["delivered", "exception", "voided", "canceled", "resolved"];
    public bool IsTerminal => TerminalStatuses.Contains(Status);
}

public enum UserRole { Admin, Staff }

/// <summary>Admin-site user (table "users": "user" is reserved in PostgreSQL).</summary>
public class User
{
    public int UserId { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public string? DisplayName { get; set; }
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class PostType
{
    public int PostTypeId { get; set; }
    public required string PostTypeTitle { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class Post
{
    public int PostId { get; set; }
    public int PostTypeId { get; set; }
    public PostType PostType { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public string? Excerpt { get; set; }
    /// <summary>HTML. Sanitised in the browser before rendering.</summary>
    public required string Body { get; set; }
    public string? BannerImage { get; set; }
    public bool Published { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

public class StorePage
{
    public int StorePageId { get; set; }
    public required string StorePageName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<StoreTextInfo> Texts { get; set; } = [];
    public List<StoreImageInfo> Images { get; set; } = [];
}

/// <summary>Editable copy for a storefront page, addressed by (page, text_code).</summary>
public class StoreTextInfo
{
    public int StoreTextInfoId { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public int StorePageId { get; set; }
    public StorePage StorePage { get; set; } = null!;
    public required string TextCode { get; set; }
    public required string TextValue { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A slot type for storefront imagery, e.g. "hero banner" 1600x700, max 3.</summary>
public class StoreImageType
{
    public int StoreImageTypeId { get; set; }
    public required string Name { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int MaxCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class StoreImageInfo
{
    public int StoreImageInfoId { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public int StorePageId { get; set; }
    public StorePage StorePage { get; set; } = null!;
    public int StoreImageTypeId { get; set; }
    public StoreImageType StoreImageType { get; set; } = null!;
    public string? Title { get; set; }
    public string? Intro { get; set; }
    public string? Link { get; set; }
    public string? LinkText { get; set; }
    public required string ImageAddress { get; set; }
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
