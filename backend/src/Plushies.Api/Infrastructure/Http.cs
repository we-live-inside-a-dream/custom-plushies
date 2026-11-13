using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Plushies.Api.Infrastructure;

/// <summary>
/// Thrown by services for problems the caller can fix (bad input, out of stock, expired coupon).
/// Turned into an RFC 7807 problem response with the given status code.
/// </summary>
public class ApiException(string message, int statusCode = StatusCodes.Status400BadRequest) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static ApiException NotFound(string what) => new($"{what} was not found.", StatusCodes.Status404NotFound);
    public static ApiException Conflict(string message) => new(message, StatusCodes.Status409Conflict);
}

public class ApiExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception exception, CancellationToken ct)
    {
        if (exception is not ApiException api) return false;
        ctx.Response.StatusCode = api.StatusCode;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = new ProblemDetails { Status = api.StatusCode, Title = api.Message },
        });
    }
}

/// <summary>Runs DataAnnotations validation on the request-body arguments of an endpoint.</summary>
public class ValidationFilter(IServiceProviderIsService services) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        foreach (var arg in ctx.Arguments)
        {
            // Only our own request DTOs: skip injected services (DbContext, CartService, ...) and framework types.
            if (arg is null || !IsOurs(arg.GetType()) || services.IsService(arg.GetType())) continue;
            var errors = new Dictionary<string, string[]>();
            Collect(arg, "", errors, depth: 0);
            if (errors.Count > 0) return Results.ValidationProblem(errors, title: errors.First().Value[0]);
        }
        return await next(ctx);
    }

    private static void Collect(object target, string prefix, Dictionary<string, string[]> errors, int depth)
    {
        if (depth > 4) return;
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(target, new ValidationContext(target), results, validateAllProperties: true);
        foreach (var r in results)
        {
            var key = prefix + JsonNamingPolicyCamel(r.MemberNames.FirstOrDefault() ?? "");
            errors[key] = [r.ErrorMessage ?? "Invalid value."];
        }
        // Walk nested request objects and lists of them (checkout.address, product.variants[2]).
        foreach (var p in target.GetType().GetProperties())
        {
            if (p.GetIndexParameters().Length > 0) continue;
            var value = p.GetValue(target);
            var name = prefix + JsonNamingPolicyCamel(p.Name);
            if (value is null or string) continue;
            if (IsOurs(p.PropertyType)) Collect(value, name + ".", errors, depth + 1);
            else if (value is System.Collections.IEnumerable list && p.PropertyType.IsGenericType && IsOurs(p.PropertyType.GetGenericArguments()[^1]))
            {
                var index = 0;
                foreach (var item in list)
                {
                    if (item is not null) Collect(item, $"{name}[{index}].", errors, depth + 1);
                    index++;
                }
            }
        }
    }

    private static bool IsOurs(Type t) => t.IsClass && t.Namespace?.StartsWith("Plushies.Api") == true;

    private static string JsonNamingPolicyCamel(string s) =>
        s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}

public static partial class Slug
{
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();

    public static string From(string text) => NonSlug().Replace(text.Trim().ToLowerInvariant(), "-").Trim('-');
}

public record Paged<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
