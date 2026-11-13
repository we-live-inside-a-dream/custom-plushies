using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Plushies.Api.Data;
using Plushies.Api.Domain;
using Plushies.Api.Features.Carts;
using Plushies.Api.Features.Catalog;
using Plushies.Api.Infrastructure;

namespace Plushies.Api.Features.Orders;

public class AddressRequest
{
    [Required(ErrorMessage = "Who should we send it to?"), MaxLength(120)] public string RecipientName { get; set; } = "";
    [Required(ErrorMessage = "Address line 1 is required."), MaxLength(200)] public string Address1 { get; set; } = "";
    [MaxLength(200)] public string? Address2 { get; set; }
    [Required(ErrorMessage = "City is required."), MaxLength(120)] public string City { get; set; } = "";
    [MaxLength(10)] public string? ProvinceCode { get; set; }
    [MaxLength(20)] public string? PostalCode { get; set; }
    [Required, StringLength(2, MinimumLength = 2, ErrorMessage = "Country must be a 2-letter code like CA.")]
    public string CountryCode { get; set; } = "";
    [MaxLength(40)] public string? Phone { get; set; }
}

public class CheckoutRequest
{
    [Required] public AddressRequest Address { get; set; } = new();
    [Range(1, int.MaxValue, ErrorMessage = "Choose a shipping method.")] public int ShippingMethodId { get; set; }
    [MaxLength(1000)] public string? Note { get; set; }
    /// <summary>Opaque token from the payment provider's browser SDK.</summary>
    [Required(ErrorMessage = "Payment details are missing.")] public string PaymentToken { get; set; } = "";
    public bool SaveAddress { get; set; }
}

public record ShippingOptionDto(int Id, string Name, string? Description, string? EstimatedDays, int PriceCents, bool Free);
public record TotalsDto(int SubtotalCents, int DiscountCents, int ShippingCents, int TaxCents, string? TaxLabel, int TotalCents);

public class CheckoutService(PlushiesDbContext db, CartService carts, IPaymentGateway payments, TimeProvider clock,
    ILogger<CheckoutService> log)
{
    /// <summary>Countries that require a province/state code for a deliverable address.</summary>
    private static readonly string[] ProvinceRequired = ["CA", "US"];

    public async Task<List<ShippingOptionDto>> ShippingOptionsAsync(string countryCode, int discountedSubtotalCents)
    {
        countryCode = countryCode.ToUpperInvariant();
        var methods = await db.ShippingMethods.AsNoTracking().Where(m => m.Active).OrderBy(m => m.DisplayOrder).ToListAsync();
        // Country-specific methods win; "*" rows are the fallback for everywhere else.
        var match = methods.Where(m => m.CountryCode == countryCode).ToList();
        if (match.Count == 0) match = methods.Where(m => m.CountryCode == "*").ToList();
        return match.Select(m =>
        {
            var free = m.FreeOverCents is { } over && discountedSubtotalCents >= over;
            return new ShippingOptionDto(m.ShippingMethodId, m.Name, m.Description, m.EstimatedDays, free ? 0 : m.PriceCents, free);
        }).ToList();
    }

    public async Task<TotalsDto> TotalsAsync(CartDto cart, string countryCode, string? provinceCode, int? shippingMethodId)
    {
        countryCode = countryCode.ToUpperInvariant();
        provinceCode = provinceCode?.ToUpperInvariant();
        var discounted = cart.SubtotalCents - cart.DiscountCents;
        var shipping = 0;
        if (shippingMethodId is { } id)
        {
            var options = await ShippingOptionsAsync(countryCode, discounted);
            shipping = (options.FirstOrDefault(o => o.Id == id)
                ?? throw new ApiException("That shipping method isn't available for this address.")).PriceCents;
        }
        var rates = await db.TaxRates.AsNoTracking().Where(t => t.CountryCode == countryCode).ToListAsync();
        var rate = rates.FirstOrDefault(t => t.ProvinceCode == provinceCode) ?? rates.FirstOrDefault(t => t.ProvinceCode == null);
        var tax = rate is null ? 0
            : (int)Math.Round((discounted + shipping) * rate.RatePercent / 100m, MidpointRounding.AwayFromZero);
        return new TotalsDto(cart.SubtotalCents, cart.DiscountCents, shipping, tax, rate?.Label, discounted + shipping + tax);
    }

    public async Task<Order> PlaceOrderAsync(HttpContext http, CheckoutRequest req)
    {
        var customerId = http.User.SubjectId();
        var a = req.Address;
        a.CountryCode = a.CountryCode.ToUpperInvariant();
        a.ProvinceCode = string.IsNullOrWhiteSpace(a.ProvinceCode) ? null : a.ProvinceCode.Trim().ToUpperInvariant();
        if (ProvinceRequired.Contains(a.CountryCode) && a.ProvinceCode is not { Length: 2 })
            throw new ApiException("Province/state must be a 2-letter code (for example ON or CA).");
        if (ProvinceRequired.Contains(a.CountryCode) && string.IsNullOrWhiteSpace(a.PostalCode))
            throw new ApiException("Postal/ZIP code is required.");

        var customer = await db.Customers.FirstAsync(c => c.CustomerId == customerId);
        var cart = await carts.ResolveAsync(http, create: false);
        if (cart is null || cart.Items.Count == 0) throw new ApiException("Your cart is empty.");

        var now = clock.GetUtcNow();
        var dto = carts.ToDto(cart);
        if (dto.Items.FirstOrDefault(l => !l.Available) is { } gone)
            throw ApiException.Conflict($"{gone.Name} is no longer available. Remove it from your cart to continue.");
        if (dto.Coupon?.Problem is { } couponProblem) throw new ApiException(couponProblem);

        var totals = await TotalsAsync(dto, a.CountryCode, a.ProvinceCode, req.ShippingMethodId);
        var method = await db.ShippingMethods.FirstAsync(m => m.ShippingMethodId == req.ShippingMethodId);

        await using var tx = await db.Database.BeginTransactionAsync();

        // Take stock with a guarded UPDATE so two shoppers can't both buy the last one.
        foreach (var line in cart.Items.Where(i => i.ProductVariantId is not null))
        {
            var taken = await db.ProductVariants
                .Where(v => v.ProductVariantId == line.ProductVariantId && v.Stock >= line.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, v => v.Stock - line.Quantity));
            if (taken == 0)
                throw ApiException.Conflict($"Sorry, {line.Product!.Name} ({line.ProductVariant!.ProductVariantValue}) just sold out.");
        }

        var order = new Order
        {
            OrderNumber = $"tmp-{Guid.NewGuid():N}",
            CustomerId = customerId,
            Status = OrderStatus.PendingPayment,
            Currency = dto.Currency,
            SubtotalCents = totals.SubtotalCents,
            DiscountCents = totals.DiscountCents,
            ShippingCents = totals.ShippingCents,
            TaxCents = totals.TaxCents,
            TaxLabel = totals.TaxLabel,
            TotalCents = totals.TotalCents,
            CouponCode = dto.Coupon?.Code,
            ShippingMethodId = method.ShippingMethodId,
            ShippingMethodName = method.Name,
            Email = customer.Email,
            ShipName = a.RecipientName.Trim(),
            ShipAddress1 = a.Address1.Trim(),
            ShipAddress2 = string.IsNullOrWhiteSpace(a.Address2) ? null : a.Address2.Trim(),
            ShipCity = a.City.Trim(),
            ShipProvinceCode = a.ProvinceCode,
            ShipPostalCode = a.PostalCode?.Trim().ToUpperInvariant(),
            ShipCountryCode = a.CountryCode,
            ShipPhone = string.IsNullOrWhiteSpace(a.Phone) ? customer.Phone : a.Phone.Trim(),
            CustomerNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        foreach (var i in cart.Items.OrderBy(i => i.CartItemId))
        {
            if (i.CustomDesign is { } d)
            {
                order.Items.Add(new OrderItem
                {
                    CustomDesignId = d.CustomDesignId, ProductName = d.Name,
                    VariantName = $"Custom {d.Design.Species} · {d.Size}" + (d.EmbroideryText is null ? "" : $" · “{d.EmbroideryText}”"),
                    Sku = $"CUSTOM-{d.Design.Species.ToUpperInvariant()}-{d.Size.ToUpperInvariant()}", Design = d.Design,
                    Quantity = i.Quantity, UnitPriceCents = d.PriceCents, TotalCents = d.PriceCents * i.Quantity,
                    WeightGrams = d.WeightGrams, CreatedAt = now,
                });
                d.Status = CustomDesignStatus.Ordered;
                d.CustomerId = customerId;
            }
            else
            {
                var v = i.ProductVariant!;
                var price = Pricing.SalePrice(v, now);
                order.Items.Add(new OrderItem
                {
                    ProductId = i.ProductId, ProductVariantId = v.ProductVariantId, ProductName = i.Product!.Name,
                    VariantName = v.ProductVariantValue, Sku = v.Sku, Design = v.Design,
                    ImageUrl = v.Images.OrderBy(x => x.DisplayOrder).FirstOrDefault()?.ImageUrl,
                    Quantity = i.Quantity, UnitPriceCents = price, TotalCents = price * i.Quantity,
                    WeightGrams = v.WeightGrams, CreatedAt = now,
                });
            }
        }
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        order.OrderNumber = $"ST-{100000 + order.OrderId}";

        if (cart.Coupon is { } coupon)
        {
            var redeemed = await db.Coupons
                .Where(c => c.CouponId == coupon.CouponId && (c.MaxRedemptions == null || c.TimesRedeemed < c.MaxRedemptions))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.TimesRedeemed, c => c.TimesRedeemed + 1));
            if (redeemed == 0) throw new ApiException("This coupon has been fully redeemed.");
        }

        var charge = await payments.ChargeAsync(new PaymentRequest(order.OrderNumber, order.TotalCents, order.Currency, req.PaymentToken, customer.Email));
        if (!charge.Succeeded)
        {
            // Rolling back releases the stock and the coupon redemption taken above.
            await tx.RollbackAsync();
            log.LogInformation("Payment declined for customer {CustomerId}: {Reason}", customerId, charge.FailureReason);
            throw new ApiException(charge.FailureReason ?? "Your payment was declined.", StatusCodes.Status402PaymentRequired);
        }

        order.Status = OrderStatus.Paid;
        order.PaidAt = now;
        order.Payments.Add(new Payment
        {
            Provider = payments.Name, ProviderReference = charge.Reference, AmountCents = order.TotalCents,
            Currency = order.Currency, Status = PaymentStatus.Succeeded, CreatedAt = now,
        });

        if (req.SaveAddress && !await db.CustomerAddresses.AnyAsync(x => x.CustomerId == customerId
                && x.Address1 == order.ShipAddress1 && x.PostalCode == order.ShipPostalCode))
        {
            var first = !await db.CustomerAddresses.AnyAsync(x => x.CustomerId == customerId);
            db.CustomerAddresses.Add(new CustomerAddress
            {
                CustomerId = customerId, RecipientName = order.ShipName, Address1 = order.ShipAddress1, Address2 = order.ShipAddress2,
                City = order.ShipCity, ProvinceCode = order.ShipProvinceCode, PostalCode = order.ShipPostalCode,
                CountryCode = order.ShipCountryCode, Phone = order.ShipPhone, IsDefault = first, CreatedAt = now, UpdatedAt = now,
            });
        }

        db.CartItems.RemoveRange(cart.Items);
        cart.CouponId = null;
        cart.UpdatedAt = now;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return order;
    }
}
