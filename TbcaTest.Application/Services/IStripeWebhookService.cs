using System;
using System.Threading;
using System.Threading.Tasks;

namespace TbcaTest.Application.Services;

public interface IStripeWebhookService
{
    Task HandleAsync(
        string eventType,
        string? customerId,
        string? subscriptionId,
        string? stripePriceId,
        string? subscriptionStatus = null,
        string? paymentIntentId = null,
        DateTime? paymentIntentCreatedAtUtc = null,
        string? hostedInvoiceUrl = null,
        CancellationToken cancellationToken = default);
}
