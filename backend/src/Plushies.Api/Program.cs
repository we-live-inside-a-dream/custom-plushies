using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Plushies.Api.Data;
using Plushies.Api.Features.Account;
using Plushies.Api.Features.Admin;
using Plushies.Api.Features.Carts;
using Plushies.Api.Features.Catalog;
using Plushies.Api.Features.Content;
using Plushies.Api.Features.Orders;
using Plushies.Api.Infrastructure;
using Plushies.Api.Shipping;
using Plushies.Api.Shipping.ChitChats;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---- Configuration -----------------------------------------------------------------------------
builder.Services.Configure<JwtOptions>(config.GetSection("Jwt"));
builder.Services.Configure<ChitChatsOptions>(config.GetSection(ChitChatsOptions.Section));
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be set to a random string of at least 32 characters (env var Jwt__Key).");

var paymentProvider = config["Payments:Provider"] ?? "test";
if (paymentProvider != "test")
    throw new InvalidOperationException($"Unknown Payments:Provider '{paymentProvider}'. Implement IPaymentGateway for it and register it in Program.cs.");
if (!builder.Environment.IsDevelopment() && !config.GetValue<bool>("Payments:AllowTestGateway"))
    throw new InvalidOperationException(
        "The test payment gateway takes no real money and is disabled outside Development. " +
        "Register a real IPaymentGateway, or set Payments:AllowTestGateway=true for a demo deployment.");

// ---- Services ----------------------------------------------------------------------------------
builder.Services.AddDbContext<PlushiesDbContext>(o => o
    // Split queries: loading an order with its items and shipments in one JOIN multiplies rows.
    .UseNpgsql(config.GetConnectionString("Plushies"), npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
    .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<IPaymentGateway, TestPaymentGateway>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<ShippingService>();
builder.Services.AddHttpClient<IChitChatsClient, ChitChatsClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHostedService<ShipmentSyncWorker>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Customer, p => p.RequireRole(Roles.Customer))
    .AddPolicy(Policies.Backoffice, p => p.RequireRole(Roles.Admin, Roles.Staff));

// Slows down password guessing: 10 sign-in/sign-up attempts per minute per client address.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("RateLimits:AuthPerMinute", 10), Window = TimeSpan.FromMinutes(1) }));
});

var corsOrigins = config.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition")));

var app = builder.Build();

// ---- Database ----------------------------------------------------------------------------------
if (config.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PlushiesDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<PasswordService>(), config,
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}

// ---- Pipeline ----------------------------------------------------------------------------------
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

var uploads = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploads);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploads),
    RequestPath = "/uploads",
    // Uploads are images only; never let a browser guess they are something executable.
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.XContentTypeOptions = "nosniff",
});

// When the React build has been copied into wwwroot (see Dockerfile), serve it from here too.
var spa = File.Exists(Path.Combine(app.Environment.WebRootPath ?? "", "index.html"));
if (spa)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", async (PlushiesDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503));
app.MapCatalog();
app.MapCart();
app.MapOrders();
app.MapAccount();
app.MapContent();
app.MapAdmin();

// Unknown /api routes are a JSON 404; everything else falls through to the SPA.
app.Map("/api/{**rest}", () => Results.Problem(statusCode: 404, title: "No such endpoint."));
if (spa) app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
