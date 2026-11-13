namespace Plushies.Api.Domain;

/// <summary>
/// The look of a plushie. Used both for ready-made variants (so the storefront can draw them)
/// and for customer-built designs. Stored as jsonb.
/// </summary>
public record PlushieDesign
{
    public string Species { get; init; } = "bear";
    public string Body { get; init; } = "#FF8A3D";
    public string Belly { get; init; } = "#FFF1D6";
    public string Eyes { get; init; } = "button";
    public string Mouth { get; init; } = "smile";
    public bool Cheeks { get; init; } = true;
    public string Accessory { get; init; } = "none";
    public string AccessoryColor { get; init; } = "#FF5DA2";
}

public class Brand
{
    public int BrandId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public List<Product> Products { get; set; } = [];
}

public class Category
{
    public int CategoryId { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<Category> Children { get; set; } = [];
    public List<ProductCategory> ProductCategories { get; set; } = [];
    public List<CategoryVariant> CategoryVariants { get; set; } = [];
}

public class Product
{
    public int ProductId { get; set; }
    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    /// <summary>ISO 4217, lower-case to match Chit Chats' value_currency ("cad" / "usd").</summary>
    public string Currency { get; set; } = "cad";
    public bool Active { get; set; } = true;
    /// <summary>Materials / care / size notes shown on the "care label".</summary>
    public string? Information { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    /// <summary>Customs data for cross-border shipments (Chit Chats line_items).</summary>
    public string? HsTariffCode { get; set; }
    public string? OriginCountry { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<ProductVariant> Variants { get; set; } = [];
    public List<ProductCategory> ProductCategories { get; set; } = [];
    public List<Review> Reviews { get; set; } = [];
}

public class ProductCategory
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public bool IsPrimary { get; set; }
}

/// <summary>A dimension a product can vary by, e.g. "Colour" (type "color") or "Size" (type "size").</summary>
public class ProductVariantType
{
    public int ProductVariantTypeId { get; set; }
    public required string Title { get; set; }
    /// <summary>How the storefront renders the picker: "color", "size" or "text".</summary>
    public string Type { get; set; } = "text";
}

/// <summary>Which variant types make sense for products in a category.</summary>
public class CategoryVariant
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int ProductVariantTypeId { get; set; }
    public ProductVariantType ProductVariantType { get; set; } = null!;
    /// <summary>Optional label override shown in admin (e.g. "Fur colour").</summary>
    public string? Field { get; set; }
}

/// <summary>
/// The sellable unit. Each variant carries its own price and stock, so "Blue Molly"
/// can be priced and counted separately from "Black Molly".
/// </summary>
public class ProductVariant
{
    public int ProductVariantId { get; set; }
    public int ProductVariantTypeId { get; set; }
    public ProductVariantType ProductVariantType { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public required string ProductVariantValue { get; set; }
    public required string Sku { get; set; }
    public int PriceCents { get; set; }
    public int Stock { get; set; }
    public int WeightGrams { get; set; } = 300;
    /// <summary>Swatch colour for "color" variant types.</summary>
    public string? ColorHex { get; set; }
    public PlushieDesign? Design { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<ProductVariantImage> Images { get; set; } = [];
    public List<ProductVariantPromotion> Promotions { get; set; } = [];
}

public class ProductVariantImage
{
    public int ProductImageId { get; set; }
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public required string ImageUrl { get; set; }
    public string? ImageThumbnailUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class Promotion
{
    public int PromotionId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    /// <summary>Percentage taken off the variant price while the promotion is running.</summary>
    public int DiscountPercent { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    /// <summary>How many products to show in the promotion strip.</summary>
    public int MaxDisplayNumber { get; set; } = 8;
    public bool DisplayHomePage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public List<ProductVariantPromotion> Variants { get; set; } = [];

    public bool IsRunning(DateTimeOffset now) => StartDate <= now && now < EndDate;
}

public class ProductVariantPromotion
{
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public int PromotionId { get; set; }
    public Promotion Promotion { get; set; } = null!;
}

public class Review
{
    public int ReviewId { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class WishList
{
    public int WishlistId { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
