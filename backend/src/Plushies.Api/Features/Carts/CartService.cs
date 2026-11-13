using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Plushies.Api.Data;
using Plushies.Api.Domain;
using Plushies.Api.Features.Catalog;
using Plushies.Api.Infrastructure;

namespace Plushies.Api.Features.Carts;


public record CartLineDto(int Id, string Kind, string Name, string? VariantName, string? Slug, PlushieDesign? Design,
    string? ImageUrl, string? EmbroideryText, int UnitPriceCents, int CompareAtCents, int Quantity, int LineTotalCents,
    int? MaxQuantity, bool Available);
public record CartCouponDto(string Code, string? Description, int DiscountCents, string? Problem);
public record CartDto(string? Token, IReadOnlyList<CartLineDto> Items, int ItemCount, int SubtotalCents,
    int DiscountCents, CartCouponDto? Coupon, string Currency);

public class CartService(PlushiesDbContext db, TimeProvider clock)
{
    public const string TokenHeader = "X-Cart-Token";
    public const int MaxLineQuantity = 20;

    private IQueryable<Cart> Carts => db.Carts
        .Include(c => c.Coupon)
        .Include(c => c.Items).ThenInclude(i => i.CustomDesign)
        .Include(c => c.Items).ThenInclude(i => i.Product)
        .Include(c => c.Items).ThenInclude(i => i.ProductVariant!.Images)
        .Include(c => c.Items).ThenInclude(i => i.ProductVariant!.ProductVariantType)
        .Include(c => c.Items).ThenInclude(i => i.ProductVariant!.Promotions).ThenInclude(p => p.Promotion)
        .AsSplitQuery();

    /// <summary>
    /// Finds the caller's cart: the customer's cart when signed in, otherwise the guest cart
    /// named by the X-Cart-Token header. With <paramref name="create"/> a cart is made when missing.
    /// </summary>
    public async Task<Cart?> ResolveAsync(HttpContext http, bool create)
    {
        var customerId = http.User.CustomerId();
        var token = http.Request.Headers[TokenHeader].FirstOrDefault();
        Cart? cart = null;

        if (customerId is not null)
            cart = await Carts.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        else if (!string.IsNullOrWhiteSpace(token))
            cart = await Carts.FirstOrDefaultAsync(c => c.SessionToken == token && c.CustomerId == null);

        if (cart is null && create)
        {
            var now = clock.GetUtcNow();
            cart = new Cart
            {
                CustomerId = customerId,
                SessionToken = customerId is null ? Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) : null,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Carts.Add(cart);
            await db.SaveChangesAsync();
        }
        return cart;
    }

    /// <summary>After sign-in, folds the guest cart (if any) into the customer's cart.</summary>
    public async Task MergeGuestCartAsync(string? token, int customerId)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var guest = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.SessionToken == token && c.CustomerId == null);
        if (guest is null) return;

        var mine = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (mine is null)
        {
            guest.CustomerId = customerId;
            guest.SessionToken = null;
        }
        else
        {
            foreach (var item in guest.Items.ToList())
            {
                var same = item.ProductVariantId is null ? null
                    : mine.Items.FirstOrDefault(i => i.ProductVariantId == item.ProductVariantId);
                if (same is not null)
                {
                    same.Quantity = Math.Min(same.Quantity + item.Quantity, MaxLineQuantity);
                    db.CartItems.Remove(item);
                }
                else
                {
                    item.CartId = mine.CartId;
                }
            }
            mine.CouponId ??= guest.CouponId;
            mine.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            db.Carts.Remove(guest);
        }
        await db.CustomDesigns.Where(d => d.SessionToken == token && d.CustomerId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.CustomerId, customerId));
        await db.SaveChangesAsync();
    }

    public CartDto ToDto(Cart? cart)
    {
        if (cart is null) return new CartDto(null, [], 0, 0, 0, null, "cad");
        var now = clock.GetUtcNow();
        var lines = cart.Items.OrderBy(i => i.CartItemId).Select(i => ToLine(i, now)).ToList();
        var subtotal = lines.Where(l => l.Available).Sum(l => l.LineTotalCents);
        CartCouponDto? coupon = null;
        if (cart.Coupon is { } c)
        {
            var (discount, problem) = CouponRules.Evaluate(c, subtotal, now);
            coupon = new CartCouponDto(c.Code, c.Description, discount, problem);
        }
        return new CartDto(cart.SessionToken, lines, lines.Sum(l => l.Quantity), subtotal, coupon?.DiscountCents ?? 0, coupon,
            cart.Items.Select(i => i.Product?.Currency).FirstOrDefault(x => x is not null) ?? "cad");
    }

    private static CartLineDto ToLine(CartItem i, DateTimeOffset now)
    {
        if (i.CustomDesign is { } d)
        {
            return new CartLineDto(i.CartItemId, "custom", d.Name, $"Custom {d.Design.Species} · {d.Size}", null, d.Design, null,
                d.EmbroideryText, d.PriceCents, d.PriceCents, i.Quantity, d.PriceCents * i.Quantity, null, true);
        }
        var v = i.ProductVariant!;
        var price = Pricing.SalePrice(v, now);
        var available = v.Active && i.Product!.Active && v.Stock > 0;
        return new CartLineDto(i.CartItemId, "product", i.Product!.Name, v.ProductVariantValue, i.Product.Slug, v.Design,
            v.Images.OrderBy(x => x.DisplayOrder).FirstOrDefault()?.ImageUrl, null, price, v.PriceCents, i.Quantity,
            price * i.Quantity, v.Stock, available);
    }

    public async Task TouchAsync(Cart cart)
    {
        cart.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
    }

    /// <summary>Reloads a cart with everything <see cref="ToDto"/> needs.</summary>
    public Task<Cart> ReloadAsync(int cartId) => Carts.FirstAsync(c => c.CartId == cartId);
}

public static class CouponRules
{
    /// <summary>Returns the discount a coupon gives on a subtotal, or (0, reason) when it does not apply.</summary>
    public static (int DiscountCents, string? Problem) Evaluate(Coupon c, int subtotalCents, DateTimeOffset now)
    {
        if (!c.Active) return (0, "This coupon is no longer active.");
        if (c.StartsAt > now) return (0, "This coupon is not active yet.");
        if (c.EndsAt <= now) return (0, "This coupon has expired.");
        if (c.MaxRedemptions is { } max && c.TimesRedeemed >= max) return (0, "This coupon has been fully redeemed.");
        if (subtotalCents < c.MinSubtotalCents)
            return (0, $"Spend {c.MinSubtotalCents / 100m:0.00} or more to use this coupon.");
        var discount = c.DiscountType == DiscountType.Percent
            ? (int)Math.Round(subtotalCents * c.DiscountValue / 100m, MidpointRounding.AwayFromZero)
            : c.DiscountValue;
        return (Math.Min(discount, subtotalCents), null);
    }
}
