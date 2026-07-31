using TbcaTest.Application.Abstractions.Persistence;
using TbcaTest.Domain.Entities;

namespace TbcaTest.Tests.TestHelpers;

public sealed class InMemoryClientRepository : IClientRepository
{
    private readonly List<Client> _clients = [];

    public IReadOnlyCollection<Client> Clients => _clients;
    public int SaveChangesCalls { get; private set; }

    public Task<Client> Create(Client client, CancellationToken cancellationToken = default)
    {
        _clients.Add(client);
        return Task.FromResult(client);
    }

    public Client Update(Client client)
    {
        var index = _clients.FindIndex(existing => existing.Id == client.Id);
        if (index >= 0)
        {
            _clients[index] = client;
        }
        else
        {
            _clients.Add(client);
        }

        return client;
    }

    public Task<Client?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(_clients.FirstOrDefault(client => client.Email == email));

    public Task<Client?> GetByFirebaseUidAsync(string firebaseUid, CancellationToken cancellationToken = default)
        => Task.FromResult(_clients.FirstOrDefault(client => client.FirebaseUid == firebaseUid));

    public Task<Client?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken cancellationToken = default)
        => Task.FromResult(_clients.FirstOrDefault(client => client.StripeCustomerId == stripeCustomerId));

    public Task<Client?> GetById(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_clients.FirstOrDefault(client => client.Id == id));

    public Task SaveChanges(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return Task.CompletedTask;
    }
}


