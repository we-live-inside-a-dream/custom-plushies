using System.ComponentModel.DataAnnotations;
using Plushies.Api.Domain;

namespace Plushies.Api.Features.Admin;

public class BrandDto
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public class CategoryAdminDto
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Title { get; set; } = "";
    [MaxLength(140)] public string? Slug { get; set; }
    public int? ParentCategoryId { get; set; }
    public string? Description { get; set; }
    /// <summary>Variant types suggested for products in this category.</summary>
    public List<int> VariantTypeIds { get; set; } = [];
}

public class VariantTypeDto
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Title { get; set; } = "";
    [Required, RegularExpression("color|size|text", ErrorMessage = "Type must be color, size or text.")]
    public string Type { get; set; } = "text";
}

public class CouponDto
{
    public int Id { get; set; }
    [Required, RegularExpression("^[A-Za-z0-9_-]{3,40}$", ErrorMessage = "Code: 3-40 letters, numbers, dashes.")]
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    [Required, RegularExpression("percent|fixed")] public string DiscountType { get; set; } = "percent";
    [Range(1, 1_000_000)] public int DiscountValue { get; set; }
    [Range(0, int.MaxValue)] public int MinSubtotalCents { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    [Range(1, int.MaxValue)] public int? MaxRedemptions { get; set; }
    public int TimesRedeemed { get; set; }
    public bool Active { get; set; } = true;
}

public class PromotionAdminDto
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    public string? Description { get; set; }
    [Range(1, 90, ErrorMessage = "Discount must be between 1% and 90%.")] public int DiscountPercent { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    [Range(1, 24)] public int MaxDisplayNumber { get; set; } = 8;
    public bool DisplayHomePage { get; set; }
    public List<int> VariantIds { get; set; } = [];
}

public class ShippingMethodDto
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    [Required, RegularExpression(@"^([A-Za-z]{2}|\*)$", ErrorMessage = "Country: 2-letter code, or * for everywhere else.")]
    public string CountryCode { get; set; } = "CA";
    [Range(0, 1_000_000)] public int PriceCents { get; set; }
    [Range(0, int.MaxValue)] public int? FreeOverCents { get; set; }
    public string? EstimatedDays { get; set; }
    public string? ChitChatsPostageType { get; set; }
    public bool Active { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public class TaxRateDto
{
    public int Id { get; set; }
    [Required, StringLength(2, MinimumLength = 2)] public string CountryCode { get; set; } = "CA";
    [MaxLength(10)] public string? ProvinceCode { get; set; }
    [Required, MaxLength(40)] public string Label { get; set; } = "";
    [Range(0, 50)] public decimal RatePercent { get; set; }
}

public class PostTypeDto
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Title { get; set; } = "";
}

public class PostAdminDto
{
    public int Id { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Choose a post type.")] public int PostTypeId { get; set; }
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(220)] public string? Slug { get; set; }
    [MaxLength(400)] public string? Excerpt { get; set; }
    [Required(ErrorMessage = "The post needs a body.")] public string Body { get; set; } = "";
    public string? BannerImage { get; set; }
    public bool Published { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

public class StoreImageTypeDto
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [Range(1, 10000)] public int Width { get; set; }
    [Range(1, 10000)] public int Height { get; set; }
    [Range(1, 100)] public int MaxCount { get; set; } = 1;
}

public class StoreImageAdminDto
{
    public int Id { get; set; }
    [Range(1, int.MaxValue)] public int StorePageId { get; set; }
    [Range(1, int.MaxValue)] public int StoreImageTypeId { get; set; }
    public string? Title { get; set; }
    public string? Intro { get; set; }
    public string? Link { get; set; }
    public string? LinkText { get; set; }
    [Required(ErrorMessage = "Upload an image first.")] public string ImageAddress { get; set; } = "";
    public int DisplayOrder { get; set; }
}

public class AdminVariantDto
{
    public int Id { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Choose a variant type.")] public int ProductVariantTypeId { get; set; }
    [Required(ErrorMessage = "Every variant needs a value, e.g. \"Blueberry\"."), MaxLength(80)] public string Value { get; set; } = "";
    [Required(ErrorMessage = "Every variant needs a SKU."), MaxLength(70)] public string Sku { get; set; } = "";
    [Range(0, 10_000_000)] public int PriceCents { get; set; }
    [Range(0, 1_000_000)] public int Stock { get; set; }
    [Range(1, 30000)] public int WeightGrams { get; set; } = 300;
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Colour must look like #FF8A3D.")] public string? ColorHex { get; set; }
    public PlushieDesign? Design { get; set; }
    public bool Active { get; set; } = true;
    public List<AdminImageDto> Images { get; set; } = [];
}

public class AdminImageDto
{
    public int Id { get; set; }
    [Required] public string Url { get; set; } = "";
}

public class AdminProductDto
{
    public int Id { get; set; }
    [Required(ErrorMessage = "The product needs a name."), MaxLength(160)] public string Name { get; set; } = "";
    [MaxLength(180)] public string? Slug { get; set; }
    public int? BrandId { get; set; }
    [Required, RegularExpression("cad|usd", ErrorMessage = "Currency must be cad or usd.")] public string Currency { get; set; } = "cad";
    public bool Active { get; set; } = true;
    public string? Description { get; set; }
    public string? Information { get; set; }
    public List<string> Tags { get; set; } = [];
    [MaxLength(12)] public string? HsTariffCode { get; set; }
    [MaxLength(2)] public string? OriginCountry { get; set; }
    public List<int> CategoryIds { get; set; } = [];
    public int? PrimaryCategoryId { get; set; }
    [MinLength(1, ErrorMessage = "Add at least one variant.")] public List<AdminVariantDto> Variants { get; set; } = [];
}

public class OrderStatusRequest
{
    [Required] public string Status { get; set; } = "";
}

public class TextsRequest
{
    [Required] public Dictionary<string, string> Texts { get; set; } = [];
}
