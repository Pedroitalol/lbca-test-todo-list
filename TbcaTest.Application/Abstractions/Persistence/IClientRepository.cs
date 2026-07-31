using TbcaTest.Domain.Entities;

namespace TbcaTest.Application.Abstractions.Persistence;

public interface IClientRepository
{
    Task<Client> Create(Client client, CancellationToken cancellationToken = default);
    Client Update(Client client);
    Task<Client?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Client?> GetByFirebaseUidAsync(string firebaseUid, CancellationToken cancellationToken = default);
    Task<Client?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken cancellationToken = default);
    Task<Client?> GetById(Guid id, CancellationToken cancellationToken = default);
    Task SaveChanges(CancellationToken cancellationToken = default);
}


