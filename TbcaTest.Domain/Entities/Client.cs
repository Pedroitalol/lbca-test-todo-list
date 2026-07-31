namespace TbcaTest.Domain.Entities;

public class Client
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? PasswordHash { get; set; }
    public string? FirebaseUid { get; set; }
    public string? AuthProvider { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? StripePriceId { get; set; }
    public string? StripeSubscriptionStatus { get; set; }
    public string? StripePendingPaymentIntentId { get; set; }
    public DateTime? StripePendingPaymentIntentCreatedAtUtc { get; set; }
    public string? StripeHostedInvoiceUrl { get; set; }
    public required Plan Plan { get; set; }
    public Roles Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}


