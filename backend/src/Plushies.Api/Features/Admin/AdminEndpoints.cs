using Microsoft.EntityFrameworkCore;
using Plushies.Api.Data;
using Plushies.Api.Domain;
using Plushies.Api.Features.Account;
using Plushies.Api.Features.Orders;
using Plushies.Api.Infrastructure;
using Plushies.Api.Shipping;

namespace Plushies.Api.Features.Admin;

public record AdminUserDto(int Id, string Email, string? DisplayName, string Role);
public record AdminAuthResponse(string Token, AdminUserDto User);
public record AdminOrderRowDto(int Id, string OrderNumber, string Status, string Customer, string Email, string Destination,
    int ItemCount, int TotalCents, string Currency, DateTimeOffset CreatedAt, string? ShipmentStatus, bool HasCustom);
public record AdminPaymentDto(string Provider, string? Reference, int AmountCents, string Status, DateTimeOffset CreatedAt);
public record AdminOrderDetailDto(OrderDto Order, int CustomerId, string CustomerName, IReadOnlyList<AdminPaymentDto> Payments,
    IReadOnlyList<ShipmentDto> Shipments, IReadOnlyList<string> NextStatuses);
public record AdminCustomerDto(int Id, string Email, string Name, string? Phone, int OrderCount, int SpentCents, DateTimeOffset CreatedAt);
public record LowStockDto(int ProductId, string Product, string Variant, string Sku, int Stock);
public record DashboardDto(int RevenueCents30d, int Orders30d, int AwaitingShipment, int InTransit, int Customers,
    IReadOnlyList<AdminOrderRowDto> RecentOrders, IReadOnlyList<LowStockDto> LowStock, IReadOnlyList<DayRevenueDto> Daily);
public record DayRevenueDto(DateOnly Day, int RevenueCents, int Orders);

public static class AdminEndpoints
{
    /// <summary>Statuses an admin may move an order to by hand. Shipping progress normally comes from Chit Chats.</summary>
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.PendingPayment] = [OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.InProduction, OrderStatus.Shipped, OrderStatus.Cancelled, OrderStatus.Refunded],
        [OrderStatus.InProduction] = [OrderStatus.Shipped, OrderStatus.Cancelled, OrderStatus.Refunded],
        [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Refunded],
        [OrderStatus.Delivered] = [OrderStatus.Refunded],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.Refunded] = [],
    };

    private static AdminOrderRowDto ToRow(Order o) => new(o.OrderId, o.OrderNumber, o.Status.ApiName(), o.ShipName, o.Email,
        $"{o.ShipCity}, {(o.ShipProvinceCode is null ? "" : o.ShipProvinceCode + ", ")}{o.ShipCountryCode}",
        o.Items.Sum(i => i.Quantity), o.TotalCents, o.Currency, o.CreatedAt,
        o.Shipments.OrderByDescending(s => s.ShipmentId).FirstOrDefault()?.Status, o.Items.Any(i => i.CustomDesignId != null));

    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/auth/login", async (LoginRequest req, PlushiesDbContext db, PasswordService passwords, TokenService tokens) =>
        {
            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null || !passwords.Verify(user.Password, req.Password))
                throw new ApiException("That email and password don't match.", StatusCodes.Status401Unauthorized);
            var role = user.Role == UserRole.Admin ? Roles.Admin : Roles.Staff;
            return new AdminAuthResponse(tokens.Issue(user.UserId, user.Email, role), new AdminUserDto(user.UserId, user.Email, user.DisplayName, role));
        }).AddEndpointFilter<ValidationFilter>().RequireRateLimiting("auth");

        var admin = app.MapGroup("/api/admin").RequireAuthorization(Policies.Backoffice);

        admin.MapGet("auth/me", async (HttpContext http, PlushiesDbContext db) =>
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == http.User.SubjectId())
                ?? throw new ApiException("Your account no longer exists.", StatusCodes.Status401Unauthorized);
            return new AdminUserDto(user.UserId, user.Email, user.DisplayName, user.Role == UserRole.Admin ? Roles.Admin : Roles.Staff);
        });

        admin.MapGet("dashboard", async (PlushiesDbContext db, TimeProvider clock) =>
        {
            var since = clock.GetUtcNow().AddDays(-30);
            OrderStatus[] counted = [OrderStatus.Paid, OrderStatus.InProduction, OrderStatus.Shipped, OrderStatus.Delivered];
            var recent = await db.Orders.AsNoTracking().Where(o => o.CreatedAt >= since && counted.Contains(o.Status))
                .Select(o => new { o.CreatedAt, o.TotalCents }).ToListAsync();
            var latest = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Shipments)
                .OrderByDescending(o => o.CreatedAt).Take(6).ToListAsync();
            var lowStock = await db.ProductVariants.AsNoTracking().Where(v => v.Active && v.Product.Active && v.Stock <= 3)
                .OrderBy(v => v.Stock).Take(8)
                .Select(v => new LowStockDto(v.ProductId, v.Product.Name, v.ProductVariantValue, v.Sku, v.Stock)).ToListAsync();
            var daily = Enumerable.Range(0, 30).Select(i => DateOnly.FromDateTime(since.AddDays(i + 1).UtcDateTime))
                .Select(day =>
                {
                    var rows = recent.Where(r => DateOnly.FromDateTime(r.CreatedAt.UtcDateTime) == day).ToList();
                    return new DayRevenueDto(day, rows.Sum(r => r.TotalCents), rows.Count);
                }).ToList();
            return new DashboardDto(recent.Sum(r => r.TotalCents), recent.Count,
                await db.Orders.CountAsync(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.InProduction),
                await db.Orders.CountAsync(o => o.Status == OrderStatus.Shipped),
                await db.Customers.CountAsync(), latest.Select(ToRow).ToList(), lowStock, daily);
        });

        // ---- Orders ---------------------------------------------------------------------------
        admin.MapGet("orders", async (PlushiesDbContext db, string? status, string? q, int page = 1, int pageSize = 25) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(page, 1);
            var query = db.Orders.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<OrderStatus>(status.Replace("_", ""), true, out var parsed)) throw new ApiException("Unknown status filter.");
                query = query.Where(o => o.Status == parsed);
            }
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                query = query.Where(o => EF.Functions.ILike(o.OrderNumber, like) || EF.Functions.ILike(o.Email, like) || EF.Functions.ILike(o.ShipName, like));
            }
            var total = await query.CountAsync();
            var rows = await query.OrderByDescending(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Include(o => o.Items).Include(o => o.Shipments).ToListAsync();
            return new Paged<AdminOrderRowDto>(rows.Select(ToRow).ToList(), total, page, pageSize);
        });

        admin.MapGet("orders/{id:int}", async (int id, PlushiesDbContext db) => await OrderDetailAsync(db, id));

        admin.MapPatch("orders/{id:int}/status", async (int id, OrderStatusRequest req, PlushiesDbContext db, TimeProvider clock) =>
        {
            if (!Enum.TryParse<OrderStatus>(req.Status.Replace("_", ""), true, out var target)) throw new ApiException("Unknown order status.");
            var order = await db.Orders.Include(o => o.Items).Include(o => o.Payments).Include(o => o.Shipments)
                .FirstOrDefaultAsync(o => o.OrderId == id) ?? throw ApiException.NotFound("Order");
            if (!Transitions[order.Status].Contains(target))
                throw ApiException.Conflict($"An order that is {order.Status.ApiName()} can't be moved to {target.ApiName()}.");

            if (target is OrderStatus.Cancelled or OrderStatus.Refunded)
            {
                if (order.Shipments.Any(s => s.Status is not ("pending" or "voided" or "postage_purchase_failed")) && target == OrderStatus.Cancelled)
                    throw ApiException.Conflict("This order has postage. Refund the shipment first, then cancel.");
                // Nothing left the building, so the plushies go back on the shelf.
                if (order.Status is OrderStatus.Paid or OrderStatus.InProduction or OrderStatus.PendingPayment)
                    foreach (var item in order.Items.Where(i => i.ProductVariantId is not null))
                        await db.ProductVariants.Where(v => v.ProductVariantId == item.ProductVariantId)
                            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, v => v.Stock + item.Quantity));
                if (target == OrderStatus.Refunded)
                    foreach (var p in order.Payments.Where(p => p.Status == PaymentStatus.Succeeded)) p.Status = PaymentStatus.Refunded;
            }
            order.Status = target;
            order.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            return await OrderDetailAsync(db, id);
        }).AddEndpointFilter<ValidationFilter>();

        // ---- Shipping (Chit Chats) ------------------------------------------------------------
        admin.MapGet("shipping/status", (ShippingService shipping, Microsoft.Extensions.Options.IOptions<Plushies.Api.Shipping.ChitChats.ChitChatsOptions> o) =>
            new { configured = shipping.IsConfigured, baseUrl = o.Value.BaseUrl, sandbox = !new Uri(o.Value.BaseUrl).Host.Equals("chitchats.com", StringComparison.OrdinalIgnoreCase) });

        admin.MapGet("orders/{id:int}/shipment-defaults", (int id, ShippingService shipping) => shipping.DefaultsAsync(id));

        var ship = admin.MapGroup("").AddEndpointFilter<ValidationFilter>();
        ship.MapPost("orders/{id:int}/shipments", async (int id, CreateShipmentInput input, ShippingService shipping, CancellationToken ct) =>
            ShippingService.ToDto(await shipping.CreateForOrderAsync(id, input, ct)));
        ship.MapPost("shipments/{id:int}/buy", async (int id, BuyPostageInput input, ShippingService shipping, CancellationToken ct) =>
            ShippingService.ToDto(await shipping.BuyPostageAsync(id, input.PostageType, ct)));
        ship.MapPost("shipments/{id:int}/sync", async (int id, ShippingService shipping, CancellationToken ct) =>
            ShippingService.ToDto(await shipping.SyncAsync(id, ct)));
        ship.MapPost("shipments/{id:int}/refund", async (int id, ShippingService shipping, CancellationToken ct) =>
            ShippingService.ToDto(await shipping.RefundAsync(id, ct)));
        ship.MapDelete("shipments/{id:int}", async (int id, ShippingService shipping, CancellationToken ct) =>
        {
            await shipping.DeleteAsync(id, ct);
            return Results.NoContent();
        });
        ship.MapGet("shipments/{id:int}/label", async (int id, ShippingService shipping, CancellationToken ct, string format = "pdf") =>
        {
            var (content, contentType, fileName) = await shipping.LabelAsync(id, format.ToLowerInvariant(), ct);
            return Results.File(content, contentType, fileName);
        });

        // ---- Customers ------------------------------------------------------------------------
        admin.MapGet("customers", async (PlushiesDbContext db, string? q) =>
        {
            var query = db.Customers.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                query = query.Where(c => EF.Functions.ILike(c.Email, like) || EF.Functions.ILike(c.FirstName + " " + c.LastName, like));
            }
            return await query.OrderByDescending(c => c.CreatedAt).Take(200)
                .Select(c => new AdminCustomerDto(c.CustomerId, c.Email, c.FirstName + " " + c.LastName, c.Phone,
                    c.Orders.Count(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.PendingPayment),
                    c.Orders.Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.PendingPayment && o.Status != OrderStatus.Refunded)
                        .Sum(o => (int?)o.TotalCents) ?? 0,
                    c.CreatedAt)).ToListAsync();
        });

        admin.MapProductAdmin();
        admin.MapSimpleResources();
        admin.MapContentAdmin();
    }

    private static async Task<AdminOrderDetailDto> OrderDetailAsync(PlushiesDbContext db, int id)
    {
        var o = await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.Shipments).Include(x => x.Payments).Include(x => x.Customer)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.OrderId == id) ?? throw ApiException.NotFound("Order");
        return new AdminOrderDetailDto(o.ToDto(), o.CustomerId, $"{o.Customer.FirstName} {o.Customer.LastName}",
            o.Payments.Select(p => new AdminPaymentDto(p.Provider, p.ProviderReference, p.AmountCents,
                System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Status.ToString()), p.CreatedAt)).ToList(),
            o.Shipments.OrderByDescending(s => s.ShipmentId).Select(ShippingService.ToDto).ToList(),
            Transitions[o.Status].Select(s => s.ApiName()).ToList());
    }
}
