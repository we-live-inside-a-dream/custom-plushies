using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Plushies.Api.Data;
using Plushies.Api.Domain;
using Plushies.Api.Infrastructure;
using Plushies.Api.Shipping.ChitChats;

namespace Plushies.Api.Shipping;

public class CreateShipmentInput
{
    [Required] public string PackageType { get; set; } = "parcel";
    [Range(1, 30000, ErrorMessage = "Weight must be between 1 g and 30 kg.")] public int WeightGrams { get; set; }
    [Range(0.1, 300)] public decimal SizeXCm { get; set; }
    [Range(0.1, 300)] public decimal SizeYCm { get; set; }
    [Range(0.1, 300)] public decimal SizeZCm { get; set; }
    public bool InsuranceRequested { get; set; }
    public bool SignatureRequested { get; set; }
    /// <summary>"today" or YYYY-MM-DD.</summary>
    public string? ShipDate { get; set; }
}

public class BuyPostageInput
{
    [Required(ErrorMessage = "Choose a postage rate.")] public string PostageType { get; set; } = "";
}

public record RateDto(string PostageType, string? Description, string? Carrier, string? DeliveryTime, string? Tracking,
    bool IsInsured, string? PaymentAmount);
public record ShipmentDto(int Id, int OrderId, string ChitChatsShipmentId, string Status, string? PostageType, string? Carrier,
    string? TrackingCode, string? TrackingUrl, int? PostageCostCents, string PackageType, int WeightGrams,
    decimal SizeXCm, decimal SizeYCm, decimal SizeZCm, bool HasLabel, DateOnly? ShipDate, DateTimeOffset? LastSyncedAt,
    DateTimeOffset CreatedAt, IReadOnlyList<RateDto> Rates);
public record ShipmentDefaultsDto(string PackageType, int WeightGrams, decimal SizeXCm, decimal SizeYCm, decimal SizeZCm,
    string? PreferredPostageType, IReadOnlyList<string> PackageTypes);

/// <summary>Turns orders into Chit Chats shipments and keeps the local copy in step with theirs.</summary>
public class ShippingService(PlushiesDbContext db, IChitChatsClient chitChats, IOptions<ChitChatsOptions> options,
    TimeProvider clock, ILogger<ShippingService> log)
{
    private readonly ChitChatsOptions _o = options.Value;

    public bool IsConfigured => chitChats.IsConfigured;

    public async Task<ShipmentDefaultsDto> DefaultsAsync(int orderId)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.ShippingMethod)
            .FirstOrDefaultAsync(o => o.OrderId == orderId) ?? throw ApiException.NotFound("Order");
        return new ShipmentDefaultsDto(_o.DefaultPackageType,
            order.Items.Sum(i => i.WeightGrams * i.Quantity) + _o.PackagingWeightGrams,
            _o.DefaultSizeXCm, _o.DefaultSizeYCm, _o.DefaultSizeZCm, order.ShippingMethod?.ChitChatsPostageType,
            ChitChatsEnums.PackageTypes);
    }

    public CreateShipmentRequest BuildRequest(Order order, CreateShipmentInput input, IReadOnlyDictionary<int, Product> products)
    {
        var currency = order.Currency.ToLowerInvariant();
        if (!ChitChatsEnums.Currencies.Contains(currency))
            throw new ApiException($"Chit Chats only accepts CAD or USD values; this order is in {order.Currency.ToUpperInvariant()}.");
        if (!ChitChatsEnums.PackageTypes.Contains(input.PackageType))
            throw new ApiException($"'{input.PackageType}' is not a Chit Chats package type.");
        if (input.ShipDate is not (null or "" or "today") && !DateOnly.TryParseExact(input.ShipDate, "yyyy-MM-dd", out _))
            throw new ApiException("Ship date must be 'today' or a date like 2026-03-31.");

        var lineItems = order.Items.OrderBy(i => i.OrderItemId).Select(i =>
        {
            var product = i.ProductId is { } pid ? products.GetValueOrDefault(pid) : null;
            return new ChitChatsLineItem
            {
                Quantity = i.Quantity,
                Description = $"{_o.ItemDescription} - {i.ProductName}",
                ValueAmount = Money(i.UnitPriceCents),
                CurrencyCode = currency,
                HsTariffCode = product?.HsTariffCode ?? _o.DefaultHsTariffCode,
                OriginCountry = product?.OriginCountry ?? _o.DefaultOriginCountry,
                SkuCode = i.Sku,
                ManufacturerInternalId = i.Sku,
                Weight = i.WeightGrams,
                WeightUnit = "g",
            };
        }).ToList();

        return new CreateShipmentRequest
        {
            Name = order.ShipName,
            Address1 = order.ShipAddress1,
            Address2 = order.ShipAddress2,
            City = order.ShipCity,
            ProvinceCode = order.ShipProvinceCode,
            PostalCode = order.ShipPostalCode,
            CountryCode = order.ShipCountryCode,
            Phone = order.ShipPhone,
            Email = order.Email,
            PackageContents = "merchandise",
            Description = _o.ItemDescription,
            // Declared value is what the customer paid for the goods: after discounts, before shipping and tax.
            Value = Money(order.SubtotalCents - order.DiscountCents),
            ValueCurrency = currency,
            OrderId = order.OrderNumber,
            OrderStore = _o.OrderStore,
            PackageType = input.PackageType,
            WeightUnit = "g",
            Weight = input.WeightGrams,
            SizeUnit = "cm",
            SizeX = input.SizeXCm,
            SizeY = input.SizeYCm,
            SizeZ = input.SizeZCm,
            InsuranceRequested = input.InsuranceRequested,
            SignatureRequested = input.SignatureRequested,
            PostageType = ChitChatsPostage.Unknown,
            ShipDate = string.IsNullOrWhiteSpace(input.ShipDate) ? "today" : input.ShipDate,
            LineItems = lineItems,
        };
    }

    /// <summary>Creates the shipment at Chit Chats with postage_type "unknown" so we get rates to choose from.</summary>
    public async Task<Shipment> CreateForOrderAsync(int orderId, CreateShipmentInput input, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Items).Include(o => o.Shipments)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, ct) ?? throw ApiException.NotFound("Order");
        if (order.Status is OrderStatus.PendingPayment or OrderStatus.Cancelled or OrderStatus.Refunded)
            throw ApiException.Conflict($"An order that is {order.Status.ToString().ToLowerInvariant()} can't be shipped.");
        if (order.Shipments.Any(s => !s.IsTerminal))
            throw ApiException.Conflict("This order already has an open shipment. Void or delete it before creating another.");

        var productIds = order.Items.Where(i => i.ProductId is not null).Select(i => i.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.ProductId)).ToDictionaryAsync(p => p.ProductId, ct);

        var remote = await CallAsync(() => chitChats.CreateShipmentAsync(BuildRequest(order, input, products), ct));

        var now = clock.GetUtcNow();
        var shipment = new Shipment
        {
            OrderId = order.OrderId, ChitChatsShipmentId = remote.Id, Status = remote.Status, PackageType = input.PackageType,
            WeightGrams = input.WeightGrams, SizeXCm = input.SizeXCm, SizeYCm = input.SizeYCm, SizeZCm = input.SizeZCm,
            CreatedAt = now, UpdatedAt = now,
        };
        Apply(shipment, remote, now);
        db.Shipments.Add(shipment);
        if (order.Status == OrderStatus.Paid)
        {
            order.Status = OrderStatus.InProduction;
            order.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        log.LogInformation("Created Chit Chats shipment {ShipmentId} for order {OrderNumber}", remote.Id, order.OrderNumber);
        return shipment;
    }

    public async Task<Shipment> BuyPostageAsync(int shipmentId, string postageType, CancellationToken ct = default)
    {
        var shipment = await LoadAsync(shipmentId, ct);
        if (shipment.Status != ChitChatsStatus.Pending && shipment.Status != ChitChatsStatus.PostagePurchaseFailed)
            throw ApiException.Conflict($"Postage can only be bought for a pending shipment (this one is {shipment.Status}).");
        var remote = await CallAsync(() => chitChats.BuyPostageAsync(shipment.ChitChatsShipmentId, postageType, ct));
        await SaveAsync(shipment, remote, ct);
        log.LogInformation("Bought {PostageType} for shipment {ShipmentId}: {Status}", postageType, shipment.ChitChatsShipmentId, remote.Status);
        return shipment;
    }

    public async Task<Shipment> SyncAsync(int shipmentId, CancellationToken ct = default)
    {
        var shipment = await LoadAsync(shipmentId, ct);
        var remote = await CallAsync(() => chitChats.GetShipmentAsync(shipment.ChitChatsShipmentId, ct));
        await SaveAsync(shipment, remote, ct);
        return shipment;
    }

    public async Task<Shipment> RefundAsync(int shipmentId, CancellationToken ct = default)
    {
        var shipment = await LoadAsync(shipmentId, ct);
        await CallAsync(async () => { await chitChats.RefundShipmentAsync(shipment.ChitChatsShipmentId, ct); return true; });
        var remote = await CallAsync(() => chitChats.GetShipmentAsync(shipment.ChitChatsShipmentId, ct));
        await SaveAsync(shipment, remote, ct);
        return shipment;
    }

    /// <summary>Deletes a shipment that has no postage yet, at Chit Chats and locally.</summary>
    public async Task DeleteAsync(int shipmentId, CancellationToken ct = default)
    {
        var shipment = await LoadAsync(shipmentId, ct);
        if (shipment.Status != ChitChatsStatus.Pending && shipment.Status != ChitChatsStatus.PostagePurchaseFailed)
            throw ApiException.Conflict("Only a shipment without postage can be deleted. Refund the postage instead to void it.");
        try
        {
            await chitChats.DeleteShipmentAsync(shipment.ChitChatsShipmentId, ct);
        }
        catch (ChitChatsException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone on their side; removing our copy is still the right outcome.
        }
        catch (ChitChatsException ex)
        {
            throw new ApiException(ex.Message, StatusCodes.Status502BadGateway);
        }
        db.Shipments.Remove(shipment);
        await db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> LabelAsync(int shipmentId, string format, CancellationToken ct = default)
    {
        var shipment = await LoadAsync(shipmentId, ct);
        var url = format switch
        {
            "pdf" => shipment.LabelPdfUrl,
            "png" => shipment.LabelPngUrl,
            "zpl" => shipment.LabelZplUrl,
            _ => throw new ApiException("Label format must be pdf, png or zpl."),
        } ?? throw ApiException.NotFound($"A {format.ToUpperInvariant()} label for this shipment");
        var (content, contentType) = await CallAsync(() => chitChats.DownloadLabelAsync(url, ct));
        return (content, contentType, $"{shipment.Order.OrderNumber}-{shipment.ChitChatsShipmentId}.{format}");
    }

    /// <summary>Refreshes every shipment that has postage and has not reached a final state. Used by the background job.</summary>
    public async Task<int> SyncOpenShipmentsAsync(CancellationToken ct = default)
    {
        var ids = await db.Shipments
            .Where(s => s.Status != ChitChatsStatus.Pending && !Shipment.TerminalStatuses.Contains(s.Status))
            .OrderBy(s => s.LastSyncedAt).Select(s => s.ShipmentId).Take(200).ToListAsync(ct);
        var synced = 0;
        foreach (var id in ids)
        {
            try
            {
                await SyncAsync(id, ct);
                synced++;
            }
            catch (ApiException ex)
            {
                log.LogWarning("Could not sync shipment {ShipmentId}: {Message}", id, ex.Message);
            }
        }
        return synced;
    }

    public static ShipmentDto ToDto(Shipment s) => new(s.ShipmentId, s.OrderId, s.ChitChatsShipmentId, s.Status, s.PostageType,
        s.Carrier, s.CarrierTrackingCode, s.TrackingUrl, s.PostageCostCents, s.PackageType, s.WeightGrams, s.SizeXCm, s.SizeYCm,
        s.SizeZCm, s.LabelPdfUrl is not null || s.LabelPngUrl is not null, s.ShipDate, s.LastSyncedAt, s.CreatedAt, Rates(s));

    private static List<RateDto> Rates(Shipment s)
    {
        if (string.IsNullOrEmpty(s.RatesJson)) return [];
        var rates = JsonSerializer.Deserialize<List<ChitChatsRate>>(s.RatesJson, ChitChatsJson.Options) ?? [];
        return rates.Select(r => new RateDto(r.PostageType, r.PostageDescription, r.PostageCarrierType, r.DeliveryTimeDescription,
            r.TrackingTypeDescription, r.IsInsured, r.PaymentAmount ?? r.PurchaseAmount)).ToList();
    }

    private async Task<Shipment> LoadAsync(int shipmentId, CancellationToken ct) =>
        await db.Shipments.Include(s => s.Order).FirstOrDefaultAsync(s => s.ShipmentId == shipmentId, ct)
        ?? throw ApiException.NotFound("Shipment");

    private async Task SaveAsync(Shipment shipment, ChitChatsShipment remote, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        Apply(shipment, remote, now);
        AdvanceOrder(shipment.Order, shipment.Status, now);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Shipment s, ChitChatsShipment r, DateTimeOffset now)
    {
        s.Status = r.Status;
        s.PostageType = r.PostageType is null or ChitChatsPostage.Unknown ? null : r.PostageType;
        s.Carrier = NullIfEmpty(r.Carrier);
        s.CarrierTrackingCode = NullIfEmpty(r.CarrierTrackingCode);
        s.TrackingUrl = NullIfEmpty(r.TrackingUrl);
        s.PostageCostCents = decimal.TryParse(r.PurchaseAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0
            ? (int)Math.Round(amount * 100) : null;
        s.LabelPngUrl = NullIfEmpty(r.PostageLabelPngUrl);
        s.LabelPdfUrl = NullIfEmpty(r.PostageLabelPdfUrl);
        s.LabelZplUrl = NullIfEmpty(r.PostageLabelZplUrl);
        s.BatchId = NullIfEmpty(r.BatchId);
        s.ShipDate = DateOnly.TryParseExact(r.ShipDate, "yyyy-MM-dd", out var date) ? date : s.ShipDate;
        // Rates are only returned while postage is still undecided; keep the last set we saw.
        if (r.Rates.Count > 0) s.RatesJson = JsonSerializer.Serialize(r.Rates, ChitChatsJson.Options);
        s.LastSyncedAt = now;
        s.UpdatedAt = now;
    }

    /// <summary>Moves the order forward as its parcel progresses. Never moves it backwards.</summary>
    private static void AdvanceOrder(Order order, string shipmentStatus, DateTimeOffset now)
    {
        OrderStatus? target = shipmentStatus switch
        {
            ChitChatsStatus.Received or ChitChatsStatus.Released or ChitChatsStatus.Inducted or ChitChatsStatus.InTransit => OrderStatus.Shipped,
            ChitChatsStatus.Delivered => OrderStatus.Delivered,
            _ => null,
        };
        if (target is null || order.Status is OrderStatus.Cancelled or OrderStatus.Refunded or OrderStatus.PendingPayment) return;
        if (target > order.Status)
        {
            order.Status = target.Value;
            order.UpdatedAt = now;
        }
    }

    private static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (ChitChatsException ex)
        {
            // Surface Chit Chats' own message (bad address, insufficient credit, ...) to the admin.
            var status = ex.StatusCode == System.Net.HttpStatusCode.BadRequest
                ? StatusCodes.Status422UnprocessableEntity : StatusCodes.Status502BadGateway;
            throw new ApiException(ex.Message, status);
        }
    }

    private static string Money(int cents) => (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

/// <summary>Periodically refreshes in-flight shipments so orders show "shipped" and "delivered" without anyone clicking.</summary>
public class ShipmentSyncWorker(IServiceScopeFactory scopes, IOptions<ChitChatsOptions> options, ILogger<ShipmentSyncWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (!o.IsConfigured || o.SyncIntervalMinutes <= 0)
        {
            log.LogInformation("Shipment sync is off (Chit Chats not configured or SyncIntervalMinutes is 0).");
            return;
        }
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(o.SyncIntervalMinutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var synced = await scope.ServiceProvider.GetRequiredService<ShippingService>().SyncOpenShipmentsAsync(stoppingToken);
                if (synced > 0) log.LogInformation("Synced {Count} shipments with Chit Chats", synced);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Shipment sync failed");
            }
        }
    }
}
