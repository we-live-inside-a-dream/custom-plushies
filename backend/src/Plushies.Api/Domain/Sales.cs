namespace Plushies.Api.Domain;

public class Customer
{
    public int CustomerId { get; set; }
    public required string Email { get; set; }
    /// <summary>Password hash (never the plain password). Column name follows the diagram.</summary>
    public required string Password { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Phone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<CustomerAddress> Addresses { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
}

/// <summary>
/// Replaces the single address columns on customer in the original diagram. Split into the
/// fields Chit Chats needs (address_1, address_2, province_code, country_code).
/// </summary>
public class CustomerAddress
{
    public int CustomerAddressId { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public required string RecipientName { get; set; }
    public required string Address1 { get; set; }
    public string? Address2 { get; set; }
    public required string City { get; set; }
    public string? ProvinceCode { get; set; }
    public string? PostalCode { get; set; }
    public required string CountryCode { get; set; }
    public string? Phone { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Cart
{
    public int CartId { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>Opaque token identifying a guest's cart (sent as X-Cart-Token).</summary>
    public string? SessionToken { get; set; }
    public int? CouponId { get; set; }
    public Coupon? Coupon { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<CartItem> Items { get; set; } = [];
}

/// <summary>A cart line is either a ready-made variant or a customer's custom design.</summary>
public class CartItem
{
    public int CartItemId { get; set; }
    public int CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public int? CustomDesignId { get; set; }
    public CustomDesign? CustomDesign { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum CustomDesignStatus { Draft, Ordered }

/// <summary>A plushie designed in the builder. Priced server-side from the builder catalogue.</summary>
public class CustomDesign
{
    public int CustomDesignId { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? SessionToken { get; set; }
    public required string Name { get; set; }
    public required PlushieDesign Design { get; set; }
    public required string Size { get; set; }
    public string? EmbroideryText { get; set; }
    public string? Notes { get; set; }
    public int PriceCents { get; set; }
    public int WeightGrams { get; set; }
    public CustomDesignStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum DiscountType { Percent, Fixed }

public class Coupon
{
    public int CouponId { get; set; }
    public required string Code { get; set; }
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; }
    /// <summary>Percent (1-100) or an amount in cents, depending on <see cref="DiscountType"/>.</summary>
    public int DiscountValue { get; set; }
    public int MinSubtotalCents { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public int? MaxRedemptions { get; set; }
    public int TimesRedeemed { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

public enum OrderStatus { PendingPayment, Paid, InProduction, Shipped, Delivered, Cancelled, Refunded }

public class Order
{
    public int OrderId { get; set; }
    /// <summary>Customer-facing reference, e.g. "ST-100042".</summary>
    public required string OrderNumber { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public OrderStatus Status { get; set; }
    public string Currency { get; set; } = "cad";
    public int SubtotalCents { get; set; }
    public int DiscountCents { get; set; }
    public int ShippingCents { get; set; }
    public int TaxCents { get; set; }
    public int TotalCents { get; set; }
    public string? CouponCode { get; set; }
    public string? TaxLabel { get; set; }

    public int? ShippingMethodId { get; set; }
    public ShippingMethod? ShippingMethod { get; set; }
    public required string ShippingMethodName { get; set; }

    // Ship-to snapshot: an order must keep the address it was placed with.
    public required string Email { get; set; }
    public required string ShipName { get; set; }
    public required string ShipAddress1 { get; set; }
    public string? ShipAddress2 { get; set; }
    public required string ShipCity { get; set; }
    public string? ShipProvinceCode { get; set; }
    public string? ShipPostalCode { get; set; }
    public required string ShipCountryCode { get; set; }
    public string? ShipPhone { get; set; }

    public string? CustomerNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }

    public List<OrderItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public List<Shipment> Shipments { get; set; } = [];
}

public class OrderItem
{
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public int? CustomDesignId { get; set; }
    public CustomDesign? CustomDesign { get; set; }
    // Snapshots, so later catalogue edits never rewrite order history.
    public required string ProductName { get; set; }
    public string? VariantName { get; set; }
    public string? Sku { get; set; }
    public PlushieDesign? Design { get; set; }
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public int UnitPriceCents { get; set; }
    public int TotalCents { get; set; }
    public int WeightGrams { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum PaymentStatus { Pending, Succeeded, Failed, Refunded }

public class Payment
{
    public int PaymentId { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public required string Provider { get; set; }
    public string? ProviderReference { get; set; }
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "cad";
    public PaymentStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
