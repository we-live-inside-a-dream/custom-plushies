using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Plushies.Api.Data;
using Plushies.Api.Domain;
using Plushies.Api.Features.Carts;
using Plushies.Api.Infrastructure;

namespace Plushies.Api.Features.Orders;

public record OrderItemDto(int Id, string ProductName, string? VariantName, string? Sku, PlushieDesign? Design,
    string? ImageUrl, int Quantity, int UnitPriceCents, int TotalCents, bool IsCustom);
public record OrderShipmentDto(int Id, string Status, string? Carrier, string? TrackingCode, string? TrackingUrl, DateOnly? ShipDate);
public record OrderAddressDto(string Name, string Address1, string? Address2, string City, string? ProvinceCode,
    string? PostalCode, string CountryCode, string? Phone);
public record OrderDto(int Id, string OrderNumber, string Status, string Currency, int SubtotalCents, int DiscountCents,
    int ShippingCents, int TaxCents, string? TaxLabel, int TotalCents, string? CouponCode, string ShippingMethodName,
    string Email, OrderAddressDto ShipTo, string? CustomerNote, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt,
    IReadOnlyList<OrderItemDto> Items, IReadOnlyList<OrderShipmentDto> Shipments);

public static class OrderMapping
{
    public static string ApiName(this OrderStatus s) => System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(s.ToString());

    public static OrderDto ToDto(this Order o) => new(o.OrderId, o.OrderNumber, o.Status.ApiName(), o.Currency,
        o.SubtotalCents, o.DiscountCents, o.ShippingCents, o.TaxCents, o.TaxLabel, o.TotalCents, o.CouponCode,
        o.ShippingMethodName, o.Email,
        new OrderAddressDto(o.ShipName, o.ShipAddress1, o.ShipAddress2, o.ShipCity, o.ShipProvinceCode, o.ShipPostalCode, o.ShipCountryCode, o.ShipPhone),
        o.CustomerNote, o.CreatedAt, o.PaidAt,
        o.Items.OrderBy(i => i.OrderItemId).Select(i => new OrderItemDto(i.OrderItemId, i.ProductName, i.VariantName, i.Sku,
            i.Design, i.ImageUrl, i.Quantity, i.UnitPriceCents, i.TotalCents, i.CustomDesignId is not null || i.Sku?.StartsWith("CUSTOM-") == true)).ToList(),
        // Customers only see shipments that actually have postage; drafts and voided labels are internal.
        o.Shipments.Where(s => s.Status is not ("pending" or "voided" or "postage_requested" or "postage_purchase_failed"))
            .OrderBy(s => s.ShipmentId)
            .Select(s => new OrderShipmentDto(s.ShipmentId, s.Status, s.Carrier, s.CarrierTrackingCode, s.TrackingUrl, s.ShipDate)).ToList());
}

public static class OrderEndpoints
{
    public static void MapOrders(this IEndpointRouteBuilder app)
    {
        var checkout = app.MapGroup("/api/checkout").RequireAuthorization(Policies.Customer).AddEndpointFilter<ValidationFilter>();

        checkout.MapGet("shipping-methods", async (string country, HttpContext http, CartService carts, CheckoutService svc) =>
        {
            var cart = carts.ToDto(await carts.ResolveAsync(http, create: false));
            return await svc.ShippingOptionsAsync(country, cart.SubtotalCents - cart.DiscountCents);
        });

        checkout.MapGet("totals", async (string country, string? province, int? shippingMethodId, HttpContext http,
            CartService carts, CheckoutService svc) =>
        {
            var cart = carts.ToDto(await carts.ResolveAsync(http, create: false));
            return await svc.TotalsAsync(cart, country, province, shippingMethodId);
        });

        checkout.MapPost("", async (CheckoutRequest req, HttpContext http, CheckoutService svc) =>
        {
            var order = await svc.PlaceOrderAsync(http, req);
            return Results.Created($"/api/orders/{order.OrderNumber}", order.ToDto());
        });

        var orders = app.MapGroup("/api/orders").RequireAuthorization(Policies.Customer);

        orders.MapGet("", async (ClaimsPrincipal user, PlushiesDbContext db) =>
        {
            var id = user.SubjectId();
            var list = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Shipments)
                .Where(o => o.CustomerId == id).OrderByDescending(o => o.CreatedAt).ToListAsync();
            return list.Select(o => o.ToDto());
        });

        orders.MapGet("{orderNumber}", async (string orderNumber, ClaimsPrincipal user, PlushiesDbContext db) =>
        {
            var id = user.SubjectId();
            // Scoped to the signed-in customer: someone else's order number is a 404, not a 403.
            var order = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Shipments)
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber && o.CustomerId == id)
                ?? throw ApiException.NotFound("Order");
            return order.ToDto();
        });
    }
}
