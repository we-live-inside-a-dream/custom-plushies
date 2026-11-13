using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Plushies.Api.Data;
using Plushies.Api.Domain;
using Plushies.Api.Features.Builder;
using Plushies.Api.Infrastructure;

namespace Plushies.Api.Features.Carts;

public class CustomDesignRequest
{
    [Required, StringLength(40, MinimumLength = 1, ErrorMessage = "Give your plushie a name (up to 40 characters).")]
    public string Name { get; set; } = "";
    [Required] public PlushieDesign Design { get; set; } = new();
    [Required] public string Size { get; set; } = "classic";
    [MaxLength(BuilderCatalog.EmbroideryMaxLength)] public string? EmbroideryText { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
}

public class AddCartItemRequest
{
    public int? ProductVariantId { get; set; }
    public CustomDesignRequest? CustomDesign { get; set; }
    [Range(1, CartService.MaxLineQuantity)] public int Quantity { get; set; } = 1;
}

public class UpdateCartItemRequest
{
    [Range(1, CartService.MaxLineQuantity)] public int Quantity { get; set; }
}

public class ApplyCouponRequest
{
    [Required, MaxLength(40)] public string Code { get; set; } = "";
}

public class QuoteRequest
{
    [Required] public PlushieDesign Design { get; set; } = new();
    [Required] public string Size { get; set; } = "classic";
    public string? EmbroideryText { get; set; }
}

public static class CartEndpoints
{
    public static void MapCart(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/builder/options", () => new
        {
            species = BuilderCatalog.Species,
            sizes = BuilderCatalog.Sizes,
            fabrics = BuilderCatalog.Fabrics,
            eyes = BuilderCatalog.Eyes,
            mouths = BuilderCatalog.Mouths,
            accessories = BuilderCatalog.Accessories,
            embroideryPriceCents = BuilderCatalog.EmbroideryPriceCents,
            embroideryMaxLength = BuilderCatalog.EmbroideryMaxLength,
        });

        app.MapPost("/api/builder/quote", (QuoteRequest req) =>
        {
            if (BuilderCatalog.Validate(req.Design, req.Size, req.EmbroideryText) is { } problem) throw new ApiException(problem);
            var (price, weight) = BuilderCatalog.Price(req.Design, req.Size, req.EmbroideryText);
            return new { priceCents = price, weightGrams = weight };
        }).AddEndpointFilter<ValidationFilter>();

        var cart = app.MapGroup("/api/cart").AddEndpointFilter<ValidationFilter>();

        cart.MapGet("", async (HttpContext http, CartService carts) =>
            carts.ToDto(await carts.ResolveAsync(http, create: false)));

        cart.MapPost("items", async (AddCartItemRequest req, HttpContext http, CartService carts,
            PlushiesDbContext db, TimeProvider clock) =>
        {
            if (req.ProductVariantId is null == req.CustomDesign is null)
                throw new ApiException("Send either productVariantId or customDesign.");
            var c = (await carts.ResolveAsync(http, create: true))!;
            var now = clock.GetUtcNow();

            if (req.ProductVariantId is { } variantId)
            {
                var variant = await db.ProductVariants.Include(v => v.Product)
                    .FirstOrDefaultAsync(v => v.ProductVariantId == variantId && v.Active && v.Product.Active)
                    ?? throw ApiException.NotFound("That plushie");
                var line = c.Items.FirstOrDefault(i => i.ProductVariantId == variantId);
                var wanted = (line?.Quantity ?? 0) + req.Quantity;
                if (wanted > variant.Stock)
                    throw ApiException.Conflict(variant.Stock == 0
                        ? $"{variant.Product.Name} ({variant.ProductVariantValue}) is sold out."
                        : $"Only {variant.Stock} of {variant.Product.Name} ({variant.ProductVariantValue}) left.");
                if (wanted > CartService.MaxLineQuantity)
                    throw new ApiException($"You can order up to {CartService.MaxLineQuantity} of one plushie at a time.");
                if (line is null)
                    db.CartItems.Add(new CartItem { CartId = c.CartId, ProductId = variant.ProductId, ProductVariantId = variantId, Quantity = req.Quantity, CreatedAt = now });
                else
                    line.Quantity = wanted;
            }
            else
            {
                var d = req.CustomDesign!;
                if (BuilderCatalog.Validate(d.Design, d.Size, d.EmbroideryText) is { } problem) throw new ApiException(problem);
                var (price, weight) = BuilderCatalog.Price(d.Design, d.Size, d.EmbroideryText);
                var design = new CustomDesign
                {
                    CustomerId = http.User.CustomerId(),
                    SessionToken = c.SessionToken,
                    Name = d.Name.Trim(),
                    Design = d.Design,
                    Size = d.Size,
                    EmbroideryText = string.IsNullOrWhiteSpace(d.EmbroideryText) ? null : d.EmbroideryText.Trim(),
                    Notes = string.IsNullOrWhiteSpace(d.Notes) ? null : d.Notes.Trim(),
                    PriceCents = price,
                    WeightGrams = weight,
                    Status = CustomDesignStatus.Draft,
                    CreatedAt = now,
                };
                db.CartItems.Add(new CartItem { CartId = c.CartId, CustomDesign = design, Quantity = req.Quantity, CreatedAt = now });
            }
            await carts.TouchAsync(c);
            return carts.ToDto(await carts.ReloadAsync(c.CartId));
        });

        cart.MapPatch("items/{id:int}", async (int id, UpdateCartItemRequest req, HttpContext http, CartService carts) =>
        {
            var c = await carts.ResolveAsync(http, create: false) ?? throw ApiException.NotFound("Cart");
            var line = c.Items.FirstOrDefault(i => i.CartItemId == id) ?? throw ApiException.NotFound("Cart item");
            if (line.ProductVariant is { } v && req.Quantity > v.Stock)
                throw ApiException.Conflict($"Only {v.Stock} left in stock.");
            line.Quantity = req.Quantity;
            await carts.TouchAsync(c);
            return carts.ToDto(c);
        });

        cart.MapDelete("items/{id:int}", async (int id, HttpContext http, CartService carts, PlushiesDbContext db) =>
        {
            var c = await carts.ResolveAsync(http, create: false) ?? throw ApiException.NotFound("Cart");
            var line = c.Items.FirstOrDefault(i => i.CartItemId == id) ?? throw ApiException.NotFound("Cart item");
            c.Items.Remove(line);
            db.CartItems.Remove(line);
            // A draft design only exists for its cart line.
            if (line.CustomDesign is { Status: CustomDesignStatus.Draft } design) db.CustomDesigns.Remove(design);
            await carts.TouchAsync(c);
            return carts.ToDto(c);
        });

        cart.MapPost("coupon", async (ApplyCouponRequest req, HttpContext http, CartService carts,
            PlushiesDbContext db, TimeProvider clock) =>
        {
            var c = (await carts.ResolveAsync(http, create: true))!;
            var code = req.Code.Trim().ToUpperInvariant();
            var coupon = await db.Coupons.FirstOrDefaultAsync(x => x.Code == code)
                ?? throw new ApiException($"We don't recognise the code \"{code}\".");
            var subtotal = carts.ToDto(c).SubtotalCents;
            if (CouponRules.Evaluate(coupon, subtotal, clock.GetUtcNow()).Problem is { } problem) throw new ApiException(problem);
            c.Coupon = coupon;
            await carts.TouchAsync(c);
            return carts.ToDto(c);
        });

        cart.MapDelete("coupon", async (HttpContext http, CartService carts) =>
        {
            var c = await carts.ResolveAsync(http, create: false);
            if (c is null) return carts.ToDto(null);
            c.Coupon = null;
            c.CouponId = null;
            await carts.TouchAsync(c);
            return carts.ToDto(c);
        });
    }
}
