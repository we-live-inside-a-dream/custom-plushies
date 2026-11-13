using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChitChatsSimulator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Plushies.Api.Data;

namespace Plushies.Tests;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL database and a local Chit Chats simulator.
/// Set TEST_POSTGRES to override the server (default: localhost, user/password "plushies").
/// </summary>
public class AppFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin@stitchling.test";
    public const string AdminPassword = "test-admin-password";
    public const string DemoEmail = "demo@stitchling.test";
    public const string DemoPassword = "plushie-demo-123";
    public const string ClientId = "123456";

    private readonly string _server = Environment.GetEnvironmentVariable("TEST_POSTGRES")
        ?? "Host=localhost;Port=5432;Username=plushies;Password=plushies";
    private readonly string _database = $"plushies_test_{Guid.NewGuid():N}";
    private WebApplication _simulator = null!;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public Simulator.State Sim { get; private set; } = null!;
    public string SimulatorUrl { get; private set; } = "";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        _simulator = Simulator.Build(["--urls", "http://127.0.0.1:0"]);
        await _simulator.StartAsync();
        SimulatorUrl = _simulator.Urls.First();
        Sim = _simulator.Services.GetRequiredService<Simulator.State>();
        Sim.PurchaseDelay = TimeSpan.FromMilliseconds(50);

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:Plushies", $"{_server};Database={_database}");
            b.UseSetting("Jwt:Key", "integration-test-signing-key-0123456789abcdef");
            b.UseSetting("Seed:DemoData", "true");
            b.UseSetting("Seed:AdminEmail", AdminEmail);
            b.UseSetting("Seed:AdminPassword", AdminPassword);
            b.UseSetting("RateLimits:AuthPerMinute", "100000");
            b.UseSetting("ChitChats:BaseUrl", $"{SimulatorUrl}/api/v1");
            b.UseSetting("ChitChats:ClientId", ClientId);
            b.UseSetting("ChitChats:AccessToken", Simulator.DefaultToken);
            b.UseSetting("ChitChats:DefaultHsTariffCode", "9503000073");
            b.UseSetting("ChitChats:SyncIntervalMinutes", "0");
        });
        // Creating a client starts the host, which migrates and seeds the new database.
        using var _ = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _simulator.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection($"{_server};Database=postgres");
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<T> DbAsync<T>(Func<PlushiesDbContext, Task<T>> query)
    {
        using var scope = Factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<PlushiesDbContext>());
    }

    public HttpClient Anonymous() => Factory.CreateClient();

    public async Task<HttpClient> AdminAsync()
    {
        var client = Factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/admin/auth/login", new { email = AdminEmail, password = AdminPassword });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Registers a brand-new customer so tests never share a cart.</summary>
    public async Task<HttpClient> NewCustomerAsync(string? cartToken = null)
    {
        var client = Factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new { email = $"t{Guid.NewGuid():N}@example.test", password = "correct-horse-battery", firstName = "Test", lastName = "Customer" }),
        };
        if (cartToken is not null) request.Headers.Add("X-Cart-Token", cartToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static object Address(string country = "CA", string? province = "ON", string? postal = "M5V 2T6") => new
    {
        recipientName = "Test Customer", address1 = "123 Maple Street", city = country == "CA" ? "Toronto" : "Springfield",
        provinceCode = province, postalCode = postal, countryCode = country, phone = "416-555-0100",
    };
}

[CollectionDefinition("app")]
public class AppCollection : ICollectionFixture<AppFixture>;

public static class HttpExtensions
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(AppFixture.Json);

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode, $"GET {url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.JsonAsync();
    }

    public static async Task<JsonElement> PostOkAsync(this HttpClient client, string url, object? body = null)
    {
        var response = await client.PostAsJsonAsync(url, body ?? new { });
        Assert.True(response.IsSuccessStatusCode, $"POST {url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.JsonAsync();
    }
}
