using Microsoft.EntityFrameworkCore;
using TbcaTest.Application.Abstractions.Persistence;
using TbcaTest.Domain.Entities;
using TbcaTest.Infra.Contexts;

namespace TbcaTest.Infra.Data.Repository;

public class ClientRepository : IClientRepository
{
    private readonly TbcaTestContext _dbContext;
    private readonly DbSet<Client> _dbSet;

    public ClientRepository(TbcaTestContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = _dbContext.Set<Client>();
    }

    public async Task<Client> Create(Client client, CancellationToken cancellationToken = default)
    {
        var result = await _dbSet.AddAsync(client, cancellationToken);
        return result.Entity;
    }

    public Task<Client?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(c => c.Email == email, cancellationToken);

    public Task<Client?> GetByFirebaseUidAsync(string firebaseUid, CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(c => c.FirebaseUid == firebaseUid, cancellationToken);

    public Task<Client?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(c => c.StripeCustomerId == stripeCustomerId, cancellationToken);

    public Client Update(Client client)
        => _dbSet.Update(client).Entity!;

    public Task<Client?> GetById(Guid id, CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    
    public async Task SaveChanges(CancellationToken cancellationToken = default) 
        => await _dbContext.SaveChangesAsync(cancellationToken);
}


