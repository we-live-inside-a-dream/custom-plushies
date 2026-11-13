namespace Plushies.Api.Features.Orders;

public record PaymentRequest(string OrderNumber, int AmountCents, string Currency, string PaymentToken, string Email);
public record PaymentResult(bool Succeeded, string? Reference, string? FailureReason);

/// <summary>
/// Seam for a real card processor. Implement this for Stripe/Square/etc. and register it in
/// Program.cs; checkout only ever sees an opaque token produced by the provider's browser SDK,
/// so card numbers never touch this server.
/// </summary>
public interface IPaymentGateway
{
    string Name { get; }
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default);
}

/// <summary>
/// Stand-in gateway for development and demos. It moves no money: "tok_test_ok" approves and
/// "tok_test_declined" declines. The API refuses to start with it outside Development unless
/// Payments:AllowTestGateway is explicitly set.
/// </summary>
public class TestPaymentGateway : IPaymentGateway
{
    public const string ApprovedToken = "tok_test_ok";
    public const string DeclinedToken = "tok_test_declined";

    public string Name => "test";

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default) =>
        Task.FromResult(request.PaymentToken switch
        {
            ApprovedToken => new PaymentResult(true, $"test_{Guid.NewGuid():N}", null),
            DeclinedToken => new PaymentResult(false, null, "Your card was declined. Try another card."),
            _ => new PaymentResult(false, null, "Unrecognised payment token."),
        });
}
