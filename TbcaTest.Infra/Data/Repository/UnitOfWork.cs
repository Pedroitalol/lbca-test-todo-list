using System.Threading.Tasks;
using TbcaTest.Application.Abstractions.Persistence;
using TbcaTest.Infra.Contexts;

namespace TbcaTest.Infra.Data.Repository
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly TbcaTestContext _context;

        public UnitOfWork(TbcaTestContext context)
        {
            _context = context;
        }

        public async Task<bool> CommitAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }
    }
}
