using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        public async Task<IDbTransaction?> BeginTransactionAsync()
        {
            if (!_context.Database.IsRelational())
                return null;

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            return await connection.BeginTransactionAsync();
        }
    }
}
