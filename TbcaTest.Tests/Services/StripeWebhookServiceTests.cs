using Microsoft.Extensions.Logging.Abstractions;
using TbcaTest.Application.Services;
using TbcaTest.Domain.Entities;
using TbcaTest.Tests.TestHelpers;

namespace TbcaTest.Tests.Services;

public class StripeWebhookServiceTests
{
    [Fact]
    public async Task HandleAsync_records_pending_payment_intent_for_created_event()
    {
        var repository = new InMemoryClientRepository();
        var client = await AddClientAsync(repository);
        var createdAt = DateTime.UtcNow.AddMinutes(-5);
        var service = new StripeWebhookService(repository, NullLogger<StripeWebhookService>.Instance);

        await service.HandleAsync(
            "payment_intent.created",
            client.StripeCustomerId,
            client.StripeSubscriptionId,
            "price_standard",
            paymentIntentId: "pi_123",
            paymentIntentCreatedAtUtc: createdAt);

        client.StripePendingPaymentIntentId.Should().Be("pi_123");
        client.StripePendingPaymentIntentCreatedAtUtc.Should().Be(createdAt);
        repository.SaveChangesCalls.Should().Be(1);
    }

    [Theory]
    [InlineData("invoice.payment_failed")]
    [InlineData("payment_intent.payment_failed")]
    [InlineData("payment_intent.canceled")]
    [InlineData("customer.subscription.deleted")]
    public async Task HandleAsync_inactivates_client_for_failed_or_cancelled_events(string eventType)
    {
        var repository = new InMemoryClientRepository();
        var client = await AddClientAsync(repository);
        var service = new StripeWebhookService(repository, NullLogger<StripeWebhookService>.Instance);

        await service.HandleAsync(eventType, client.StripeCustomerId, client.StripeSubscriptionId, "price_standard");

        client.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_stores_invoice_link_for_invoice_finalized()
    {
        var repository = new InMemoryClientRepository();
        var client = await AddClientAsync(repository);
        var service = new StripeWebhookService(repository, NullLogger<StripeWebhookService>.Instance);

        await service.HandleAsync(
            "invoice.finalized",
            client.StripeCustomerId,
            client.StripeSubscriptionId,
            "price_standard",
            hostedInvoiceUrl: "https://billing.stripe.com/invoice");

        client.StripeHostedInvoiceUrl.Should().Be("https://billing.stripe.com/invoice");
        client.IsActive.Should().BeTrue();
    }

    private static async Task<Client> AddClientAsync(InMemoryClientRepository repository)
    {
        var client = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Client",
            Email = "client@example.com",
            Plan = Plan.Standard,
            Role = Roles.Client,
            IsActive = true,
            StripeCustomerId = "cus_123",
            StripeSubscriptionId = "sub_123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.Create(client);
        return client;
    }
}


