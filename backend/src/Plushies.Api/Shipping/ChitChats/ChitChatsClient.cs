using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Plushies.Api.Shipping.ChitChats;

public class ChitChatsOptions
{
    public const string Section = "ChitChats";

    /// <summary>
    /// API root without the client id. Production: https://chitchats.com/api/v1 —
    /// sandbox: https://staging.chitchats.com/api/v1.
    /// </summary>
    public string BaseUrl { get; set; } = "https://staging.chitchats.com/api/v1";
    public string ClientId { get; set; } = "";
    /// <summary>From Chit Chats → Settings → Developer → API Access Tokens. Treat like a password.</summary>
    public string AccessToken { get; set; } = "";

    /// <summary>Sent as order_store. Must be one of Chit Chats' store types; "other" for this shop.</summary>
    public string OrderStore { get; set; } = "other";
    /// <summary>Customs description prefix for each line item, e.g. "Plush toy".</summary>
    public string ItemDescription { get; set; } = "Plush toy";
    /// <summary>HS tariff code used when a product has none. Confirm the right code with your customs broker.</summary>
    public string? DefaultHsTariffCode { get; set; }
    public string DefaultOriginCountry { get; set; } = "CA";

    public string DefaultPackageType { get; set; } = "parcel";
    public decimal DefaultSizeXCm { get; set; } = 30;
    public decimal DefaultSizeYCm { get; set; } = 25;
    public decimal DefaultSizeZCm { get; set; } = 15;
    /// <summary>Box, tissue and filler added on top of the items' own weight.</summary>
    public int PackagingWeightGrams { get; set; } = 80;

    /// <summary>How often the background job refreshes in-flight shipments. 0 disables it.</summary>
    public int SyncIntervalMinutes { get; set; } = 30;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(AccessToken);
}

public class ChitChatsException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

public interface IChitChatsClient
{
    bool IsConfigured { get; }
    Task<ChitChatsShipment> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken ct = default);
    Task<ChitChatsShipment> GetShipmentAsync(string id, CancellationToken ct = default);
    Task<ChitChatsShipment> RefreshShipmentAsync(string id, RefreshShipmentRequest request, CancellationToken ct = default);
    /// <summary>Requests postage, then polls until the shipment leaves postage_requested.</summary>
    Task<ChitChatsShipment> BuyPostageAsync(string id, string postageType, CancellationToken ct = default);
    Task RefundShipmentAsync(string id, CancellationToken ct = default);
    Task DeleteShipmentAsync(string id, CancellationToken ct = default);
    Task<(byte[] Content, string ContentType)> DownloadLabelAsync(string labelUrl, CancellationToken ct = default);
}

public class ChitChatsClient(HttpClient http, IOptions<ChitChatsOptions> options, ILogger<ChitChatsClient> log) : IChitChatsClient
{
    private readonly ChitChatsOptions _o = options.Value;

    /// <summary>How long to wait for a postage purchase to settle before giving up on polling.</summary>
    public TimeSpan BuyTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan BuyPollInterval { get; set; } = TimeSpan.FromMilliseconds(750);

    public bool IsConfigured => _o.IsConfigured;

    private string Url(string path) =>
        $"{_o.BaseUrl.TrimEnd('/')}/clients/{Uri.EscapeDataString(_o.ClientId)}/{path}";

    public async Task<ChitChatsShipment> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, Url("shipments"), request, ct);
        var shipment = await ReadShipmentAsync(response, ct);
        if (shipment is not null) return shipment;
        // The spec guarantees a Location header; fall back to it if the body is ever empty.
        var id = response.Headers.Location?.ToString().TrimEnd('/').Split('/').LastOrDefault();
        if (string.IsNullOrEmpty(id)) throw new ChitChatsException("Chit Chats created the shipment but returned no id.");
        return await GetShipmentAsync(id, ct);
    }

    public async Task<ChitChatsShipment> GetShipmentAsync(string id, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, Url($"shipments/{Uri.EscapeDataString(id)}"), null, ct);
        return await ReadShipmentAsync(response, ct) ?? throw new ChitChatsException("Chit Chats returned an empty shipment.");
    }

    public async Task<ChitChatsShipment> RefreshShipmentAsync(string id, RefreshShipmentRequest request, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Patch, Url($"shipments/{Uri.EscapeDataString(id)}/refresh"), request, ct);
        return await ReadShipmentAsync(response, ct) ?? await GetShipmentAsync(id, ct);
    }

    public async Task<ChitChatsShipment> BuyPostageAsync(string id, string postageType, CancellationToken ct = default)
    {
        using (await SendAsync(HttpMethod.Patch, Url($"shipments/{Uri.EscapeDataString(id)}/buy"),
                   new BuyPostageRequest { PostageType = postageType }, ct)) { }

        // A 200 only means the purchase was requested. Poll until it settles, as the docs describe.
        var deadline = DateTime.UtcNow + BuyTimeout;
        while (true)
        {
            var shipment = await GetShipmentAsync(id, ct);
            if (shipment.Status == ChitChatsStatus.PostagePurchaseFailed)
                throw new ChitChatsException("Chit Chats could not purchase that postage. Check the account balance and try again.");
            if (shipment.Status != ChitChatsStatus.PostageRequested || DateTime.UtcNow >= deadline) return shipment;
            await Task.Delay(BuyPollInterval, ct);
        }
    }

    public async Task RefundShipmentAsync(string id, CancellationToken ct = default)
    {
        using var _ = await SendAsync(HttpMethod.Patch, Url($"shipments/{Uri.EscapeDataString(id)}/refund"), null, ct);
    }

    public async Task DeleteShipmentAsync(string id, CancellationToken ct = default)
    {
        using var _ = await SendAsync(HttpMethod.Delete, Url($"shipments/{Uri.EscapeDataString(id)}"), null, ct);
    }

    public async Task<(byte[] Content, string ContentType)> DownloadLabelAsync(string labelUrl, CancellationToken ct = default)
    {
        // Label URLs come back from the API and are stored; only ever send our token to Chit Chats itself.
        var target = new Uri(labelUrl, UriKind.Absolute);
        var api = new Uri(_o.BaseUrl);
        if (target.Scheme != api.Scheme || !string.Equals(target.Host, api.Host, StringComparison.OrdinalIgnoreCase) || target.Port != api.Port)
            throw new ChitChatsException("Refusing to download a label from a host other than the configured Chit Chats server.");
        using var response = await SendAsync(HttpMethod.Get, labelUrl, null, ct);
        return (await response.Content.ReadAsByteArrayAsync(ct),
            response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        if (!_o.IsConfigured)
            throw new ChitChatsException("Chit Chats is not configured. Set ChitChats:ClientId and ChitChats:AccessToken.");

        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, url);
            // Chit Chats expects the bare token, with no "Bearer" scheme.
            request.Headers.TryAddWithoutValidation("Authorization", _o.AccessToken);
            if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), options: ChitChatsJson.Options);

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, ct);
            }
            catch (HttpRequestException ex)
            {
                throw new ChitChatsException($"Could not reach Chit Chats: {ex.Message}");
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new ChitChatsException("Chit Chats took too long to respond.");
            }

            if (response.IsSuccessStatusCode) return response;

            // 2,000 requests per 5 minutes; a 429 says how long to wait in Retry-After.
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxAttempts)
            {
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
                response.Dispose();
                if (wait <= TimeSpan.FromSeconds(15))
                {
                    log.LogWarning("Chit Chats rate limit hit; retrying in {Seconds}s", wait.TotalSeconds);
                    await Task.Delay(wait, ct);
                    continue;
                }
                throw new ChitChatsException($"Chit Chats rate limit reached. Try again in {Math.Ceiling(wait.TotalSeconds)} seconds.", HttpStatusCode.TooManyRequests);
            }

            var message = await ReadErrorAsync(response, ct);
            var status = response.StatusCode;
            response.Dispose();
            throw new ChitChatsException(status switch
            {
                HttpStatusCode.Unauthorized => "Chit Chats rejected the credentials. Check the client id and access token.",
                HttpStatusCode.NotFound => "Chit Chats could not find that shipment.",
                _ => message ?? $"Chit Chats returned {(int)status}.",
            }, status);
        }
    }

    private static async Task<ChitChatsShipment?> ReadShipmentAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            return JsonSerializer.Deserialize<ShipmentEnvelope>(text, ChitChatsJson.Options)?.Shipment;
        }
        catch (JsonException ex)
        {
            throw new ChitChatsException($"Could not read Chit Chats' response: {ex.Message}");
        }
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<ChitChatsErrorEnvelope>(text, ChitChatsJson.Options)?.Error?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
