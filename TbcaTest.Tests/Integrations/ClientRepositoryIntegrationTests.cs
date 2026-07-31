using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TbcaTest.Domain.Entities;
using TbcaTest.Infra.Contexts;
using TbcaTest.Infra.Data.Repository;

namespace TbcaTest.Tests.Integrations;

public class ClientRepositoryIntegrationTests
{
    [Fact]
    public async Task Repository_persists_and_loads_client_by_stripe_customer_id()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TbcaTestContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new TbcaTestContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using (var writeContext = new TbcaTestContext(options))
        {
            var repository = new ClientRepository(writeContext);
            await repository.Create(new Client
            {
                Id = Guid.NewGuid(),
                Name = "Integration Client",
                Email = "integration@example.com",
                Plan = Plan.Standard,
                Role = Roles.Client,
                IsActive = true,
                StripeCustomerId = "cus_integration",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await repository.SaveChanges();
        }

        await using var readContext = new TbcaTestContext(options);
        var readRepository = new ClientRepository(readContext);

        var client = await readRepository.GetByStripeCustomerIdAsync("cus_integration");

        client.Should().NotBeNull();
        client!.Email.Should().Be("integration@example.com");
    }
}


