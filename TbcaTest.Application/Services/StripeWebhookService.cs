using Microsoft.Extensions.Logging;
using TbcaTest.Application.Abstractions.Persistence;

namespace TbcaTest.Application.Services;

public class StripeWebhookService(
    IClientRepository clientRepository,
    ILogger<StripeWebhookService> logger)
{
    public async Task HandleAsync(
        string eventType,
        string? customerId,
        string? subscriptionId,
        string? stripePriceId,
        string? subscriptionStatus = null,
        string? paymentIntentId = null,
        DateTime? paymentIntentCreatedAtUtc = null,
        string? hostedInvoiceUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            logger.LogInformation("Stripe webhook ignored because customer id is empty. eventType={EventType}", eventType);
            return;
        }

        var client = await clientRepository.GetByStripeCustomerIdAsync(customerId, cancellationToken);
        if (client is null)
        {
            logger.LogInformation("Stripe webhook ignored because no client matched customer id. eventType={EventType}", eventType);
            return;
        }

        switch (eventType)
        {
            case "payment_intent.created" when !string.IsNullOrWhiteSpace(paymentIntentId)
                                              && paymentIntentCreatedAtUtc.HasValue:
                client.StripePendingPaymentIntentId = paymentIntentId;
                client.StripePendingPaymentIntentCreatedAtUtc = paymentIntentCreatedAtUtc;
                break;

            case "payment_intent.succeeded":
                client.IsActive = true;
                client.StripePendingPaymentIntentId = null;
                client.StripePendingPaymentIntentCreatedAtUtc = null;
                break;

            case "invoice.finalized":
                client.StripeHostedInvoiceUrl = string.IsNullOrWhiteSpace(hostedInvoiceUrl)
                    ? client.StripeHostedInvoiceUrl
                    : hostedInvoiceUrl;
                break;

            case "invoice.payment_failed":
                client.StripeHostedInvoiceUrl = string.IsNullOrWhiteSpace(hostedInvoiceUrl)
                    ? client.StripeHostedInvoiceUrl
                    : hostedInvoiceUrl;
                client.IsActive = false;
                break;

            case "payment_intent.payment_failed":
            case "payment_intent.canceled":
            case "customer.subscription.deleted":
                client.IsActive = false;
                client.StripePendingPaymentIntentId = null;
                client.StripePendingPaymentIntentCreatedAtUtc = null;
                break;

            case "customer.subscription.updated":
                client.IsActive = subscriptionStatus is not "canceled" and not "past_due" and not "unpaid" and not "incomplete" and not "incomplete_expired";
                break;
        }

        client.StripeSubscriptionId = string.IsNullOrWhiteSpace(subscriptionId) ? client.StripeSubscriptionId : subscriptionId;
        client.StripePriceId = string.IsNullOrWhiteSpace(stripePriceId) ? client.StripePriceId : stripePriceId;
        client.StripeSubscriptionStatus = string.IsNullOrWhiteSpace(subscriptionStatus) ? client.StripeSubscriptionStatus : subscriptionStatus;
        client.UpdatedAt = DateTime.UtcNow;

        clientRepository.Update(client);
        await clientRepository.SaveChanges(cancellationToken);
    }
}


