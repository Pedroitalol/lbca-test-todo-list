using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using TbcaTest.Api.Responses;
using TbcaTest.Application.Services;
using TbcaTest.CrossCutting.Configuration;

namespace TbcaTest.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("webhooks")]
public class WebhooksController(
    ILogger<WebhooksController> logger,
    StripeWebhookService stripeWebhookService,
    IOptions<StripeOptions> stripeOptions) : ControllerBase
{
    private readonly StripeOptions _stripeOptions = stripeOptions.Value;

    [HttpPost("stripe")]
    public async Task<IActionResult> HandleStripeWebhook(CancellationToken cancellationToken)
    {
        HttpContext.Request.EnableBuffering();

        string payload;
        using (var reader = new StreamReader(
                   HttpContext.Request.Body,
                   Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: false,
                   leaveOpen: true))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }

        HttpContext.Request.Body.Position = 0;

        try
        {
            var signatureHeader = Request.Headers["Stripe-Signature"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(signatureHeader))
            {
                logger.LogWarning("Stripe webhook received without signature header.");
                return ApiResponseFactory.StatusCode(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    "Missing Stripe signature.",
                    "Validate Stripe webhook.",
                    "billing");
            }

            var stripeEvent = EventUtility.ConstructEvent(
                payload,
                signatureHeader,
                _stripeOptions.WebhookSecret,
                throwOnApiVersionMismatch: false);

            logger.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

            await ProcessStripeEventAsync(stripeEvent, cancellationToken);

            return Ok();
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe webhook validation error.");
            return ApiResponseFactory.StatusCode(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Invalid Stripe webhook signature.",
                "Validate Stripe webhook.",
                "billing");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stripe webhook processing error.");
            return ApiResponseFactory.StatusCode(
                HttpContext,
                StatusCodes.Status500InternalServerError,
                "Stripe webhook could not be processed.",
                "Process Stripe webhook.",
                "billing");
        }
    }

    private async Task ProcessStripeEventAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var webhookData = ExtractStripeWebhookData(stripeEvent);

        await stripeWebhookService.HandleAsync(
            stripeEvent.Type,
            webhookData.CustomerId,
            webhookData.SubscriptionId,
            webhookData.StripePriceId,
            webhookData.SubscriptionStatus,
            webhookData.PaymentIntentId,
            webhookData.PaymentIntentCreatedAtUtc,
            webhookData.HostedInvoiceUrl,
            cancellationToken);

        logger.LogInformation(
            "Stripe event processed: {EventType}. CustomerId: {CustomerId}, SubscriptionId: {SubscriptionId}, Status: {SubscriptionStatus}, PriceId: {StripePriceId}, PaymentIntentId: {PaymentIntentId}, PaymentIntentCreatedAtUtc: {PaymentIntentCreatedAtUtc}, HostedInvoiceUrlPresent: {HostedInvoiceUrlPresent}",
            stripeEvent.Type,
            webhookData.CustomerId,
            webhookData.SubscriptionId,
            webhookData.SubscriptionStatus,
            webhookData.StripePriceId,
            webhookData.PaymentIntentId,
            webhookData.PaymentIntentCreatedAtUtc,
            !string.IsNullOrWhiteSpace(webhookData.HostedInvoiceUrl));
    }

    private static StripeWebhookData ExtractStripeWebhookData(Event stripeEvent)
        => stripeEvent.Type switch
        {
            "invoice.finalized" or "invoice.payment_failed"
                => ExtractFromInvoice(stripeEvent.Data.Object as Invoice),
            "customer.subscription.deleted" or "customer.subscription.updated"
                => ExtractFromSubscription(stripeEvent.Data.Object as Subscription),
            "payment_intent.created" or "payment_intent.succeeded" or "payment_intent.payment_failed" or "payment_intent.canceled"
                => ExtractFromPaymentIntent(stripeEvent.Data.Object as PaymentIntent),
            _ => new StripeWebhookData()
        };

    private static StripeWebhookData ExtractFromInvoice(Invoice? invoice)
        => new()
        {
            CustomerId = invoice?.CustomerId,
            SubscriptionId = invoice?.Parent?.SubscriptionDetails?.SubscriptionId,
            HostedInvoiceUrl = invoice?.HostedInvoiceUrl
        };

    private static StripeWebhookData ExtractFromSubscription(Subscription? subscription)
        => new()
        {
            CustomerId = subscription?.CustomerId,
            SubscriptionId = subscription?.Id,
            SubscriptionStatus = subscription?.Status,
            StripePriceId = subscription?.Items?.Data?.FirstOrDefault()?.Price?.Id
        };

    private static StripeWebhookData ExtractFromPaymentIntent(PaymentIntent? paymentIntent)
        => new()
        {
            CustomerId = paymentIntent?.CustomerId,
            SubscriptionStatus = paymentIntent?.Status,
            PaymentIntentId = paymentIntent?.Id,
            PaymentIntentCreatedAtUtc = paymentIntent?.Created
        };

    private sealed class StripeWebhookData
    {
        public string? CustomerId { get; init; }
        public string? SubscriptionId { get; init; }
        public string? SubscriptionStatus { get; init; }
        public string? StripePriceId { get; init; }
        public string? PaymentIntentId { get; init; }
        public DateTime? PaymentIntentCreatedAtUtc { get; init; }
        public string? HostedInvoiceUrl { get; init; }
    }
}


