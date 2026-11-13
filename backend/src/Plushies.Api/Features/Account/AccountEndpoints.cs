using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Plushies.Api.Data;
using Plushies.Api.Domain;
using Plushies.Api.Features.Carts;
using Plushies.Api.Features.Orders;
using Plushies.Api.Infrastructure;

namespace Plushies.Api.Features.Account;

public class RegisterRequest
{
    [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address."), MaxLength(254)] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Choose a password."), StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = "";
    [Required(ErrorMessage = "First name is required."), MaxLength(80)] public string FirstName { get; set; } = "";
    [Required(ErrorMessage = "Last name is required."), MaxLength(80)] public string LastName { get; set; } = "";
}

public class LoginRequest
{
    [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address.")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Enter your password.")] public string Password { get; set; } = "";
}

public class ProfileRequest
{
    [Required(ErrorMessage = "First name is required."), MaxLength(80)] public string FirstName { get; set; } = "";
    [Required(ErrorMessage = "Last name is required."), MaxLength(80)] public string LastName { get; set; } = "";
    [MaxLength(40)] public string? Phone { get; set; }
}

public class SaveAddressRequest : AddressRequest
{
    public bool IsDefault { get; set; }
}

public record CustomerDto(int Id, string Email, string FirstName, string LastName, string? Phone);
public record AuthResponse(string Token, CustomerDto Customer);
public record AddressDto(int Id, string RecipientName, string Address1, string? Address2, string City, string? ProvinceCode,
    string? PostalCode, string CountryCode, string? Phone, bool IsDefault);

public static class AccountEndpoints
{
    private static CustomerDto ToDto(this Customer c) => new(c.CustomerId, c.Email, c.FirstName, c.LastName, c.Phone);
    private static AddressDto ToDto(this CustomerAddress a) => new(a.CustomerAddressId, a.RecipientName, a.Address1, a.Address2,
        a.City, a.ProvinceCode, a.PostalCode, a.CountryCode, a.Phone, a.IsDefault);

    public static void MapAccount(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").AddEndpointFilter<ValidationFilter>();

        auth.MapPost("register", async (RegisterRequest req, HttpContext http, PlushiesDbContext db, PasswordService passwords,
            TokenService tokens, CartService carts, TimeProvider clock) =>
        {
            var email = req.Email.Trim().ToLowerInvariant();
            if (await db.Customers.AnyAsync(c => c.Email == email))
                throw ApiException.Conflict("An account with that email already exists. Try signing in instead.");
            var now = clock.GetUtcNow();
            var customer = new Customer
            {
                Email = email, Password = passwords.Hash(req.Password), FirstName = req.FirstName.Trim(),
                LastName = req.LastName.Trim(), CreatedAt = now, UpdatedAt = now,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            await carts.MergeGuestCartAsync(http.Request.Headers[CartService.TokenHeader].FirstOrDefault(), customer.CustomerId);
            return new AuthResponse(tokens.Issue(customer.CustomerId, customer.Email, Roles.Customer), customer.ToDto());
        }).RequireRateLimiting("auth");

        auth.MapPost("login", async (LoginRequest req, HttpContext http, PlushiesDbContext db, PasswordService passwords,
            TokenService tokens, CartService carts) =>
        {
            var email = req.Email.Trim().ToLowerInvariant();
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email == email);
            // Same message either way so the form can't be used to discover which emails have accounts.
            if (customer is null || !passwords.Verify(customer.Password, req.Password))
                throw new ApiException("That email and password don't match.", StatusCodes.Status401Unauthorized);
            await carts.MergeGuestCartAsync(http.Request.Headers[CartService.TokenHeader].FirstOrDefault(), customer.CustomerId);
            return new AuthResponse(tokens.Issue(customer.CustomerId, customer.Email, Roles.Customer), customer.ToDto());
        }).RequireRateLimiting("auth");

        var account = app.MapGroup("/api/account").RequireAuthorization(Policies.Customer).AddEndpointFilter<ValidationFilter>();

        account.MapGet("profile", async (ClaimsPrincipal user, PlushiesDbContext db) =>
            (await db.Customers.AsNoTracking().FirstAsync(c => c.CustomerId == user.SubjectId())).ToDto());

        account.MapPut("profile", async (ProfileRequest req, ClaimsPrincipal user, PlushiesDbContext db, TimeProvider clock) =>
        {
            var c = await db.Customers.FirstAsync(x => x.CustomerId == user.SubjectId());
            c.FirstName = req.FirstName.Trim();
            c.LastName = req.LastName.Trim();
            c.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
            c.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            return c.ToDto();
        });

        account.MapGet("addresses", async (ClaimsPrincipal user, PlushiesDbContext db) =>
        {
            var id = user.SubjectId();
            var list = await db.CustomerAddresses.AsNoTracking().Where(a => a.CustomerId == id)
                .OrderByDescending(a => a.IsDefault).ThenBy(a => a.CustomerAddressId).ToListAsync();
            return list.Select(a => a.ToDto());
        });

        account.MapPost("addresses", async (SaveAddressRequest req, ClaimsPrincipal user, PlushiesDbContext db, TimeProvider clock) =>
        {
            var id = user.SubjectId();
            var now = clock.GetUtcNow();
            var first = !await db.CustomerAddresses.AnyAsync(a => a.CustomerId == id);
            var address = new CustomerAddress
            {
                CustomerId = id, RecipientName = "", Address1 = "", City = "", CountryCode = "", CreatedAt = now,
            };
            Apply(address, req, now);
            address.IsDefault = first || req.IsDefault;
            if (address.IsDefault) await ClearDefaultAsync(db, id);
            db.CustomerAddresses.Add(address);
            await db.SaveChangesAsync();
            return address.ToDto();
        });

        account.MapPut("addresses/{addressId:int}", async (int addressId, SaveAddressRequest req, ClaimsPrincipal user,
            PlushiesDbContext db, TimeProvider clock) =>
        {
            var id = user.SubjectId();
            var address = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.CustomerAddressId == addressId && a.CustomerId == id)
                ?? throw ApiException.NotFound("Address");
            Apply(address, req, clock.GetUtcNow());
            if (req.IsDefault && !address.IsDefault)
            {
                await ClearDefaultAsync(db, id);
                address.IsDefault = true;
            }
            await db.SaveChangesAsync();
            return address.ToDto();
        });

        account.MapDelete("addresses/{addressId:int}", async (int addressId, ClaimsPrincipal user, PlushiesDbContext db) =>
        {
            var id = user.SubjectId();
            await db.CustomerAddresses.Where(a => a.CustomerAddressId == addressId && a.CustomerId == id).ExecuteDeleteAsync();
            return Results.NoContent();
        });
    }

    private static void Apply(CustomerAddress a, AddressRequest req, DateTimeOffset now)
    {
        a.RecipientName = req.RecipientName.Trim();
        a.Address1 = req.Address1.Trim();
        a.Address2 = string.IsNullOrWhiteSpace(req.Address2) ? null : req.Address2.Trim();
        a.City = req.City.Trim();
        a.ProvinceCode = string.IsNullOrWhiteSpace(req.ProvinceCode) ? null : req.ProvinceCode.Trim().ToUpperInvariant();
        a.PostalCode = string.IsNullOrWhiteSpace(req.PostalCode) ? null : req.PostalCode.Trim().ToUpperInvariant();
        a.CountryCode = req.CountryCode.ToUpperInvariant();
        a.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        a.UpdatedAt = now;
    }

    private static Task ClearDefaultAsync(PlushiesDbContext db, int customerId) =>
        db.CustomerAddresses.Where(a => a.CustomerId == customerId && a.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false));
}
