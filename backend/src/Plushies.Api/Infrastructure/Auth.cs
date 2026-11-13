using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Plushies.Api.Infrastructure;

public class JwtOptions
{
    public string Issuer { get; set; } = "plushies";
    public string Audience { get; set; } = "plushies";
    /// <summary>HMAC signing key. Must be at least 32 characters; set via Jwt__Key in production.</summary>
    public string Key { get; set; } = "";
    public int ExpiryHours { get; set; } = 12;
}

public static class Roles
{
    public const string Customer = "customer";
    public const string Admin = "admin";
    public const string Staff = "staff";
}

public static class Policies
{
    public const string Customer = "Customer";
    /// <summary>Anyone who may use the admin site (admin or staff).</summary>
    public const string Backoffice = "Backoffice";
}

public class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _o = options.Value;

    public string Issue(int subjectId, string email, string role)
    {
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_o.Issuer, _o.Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, subjectId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.Role, role),
            ],
            expires: DateTime.UtcNow.AddHours(_o.ExpiryHours),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class PasswordService
{
    private static readonly object Dummy = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(Dummy, password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(Dummy, hash, password) != PasswordVerificationResult.Failed;
}

public static class ClaimsExtensions
{
    /// <summary>The numeric id in the token's subject claim (customer_id or user_id, depending on role).</summary>
    public static int SubjectId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static int? CustomerId(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && user.IsInRole(Roles.Customer) ? user.SubjectId() : null;
}
