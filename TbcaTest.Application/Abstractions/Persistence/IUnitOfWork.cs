using System.Threading.Tasks;

namespace TbcaTest.Application.Abstractions.Persistence
{
    public interface IUnitOfWork
    {
        Task<bool> CommitAsync();
        Task<System.Data.IDbTransaction?> BeginTransactionAsync();
    }
}
