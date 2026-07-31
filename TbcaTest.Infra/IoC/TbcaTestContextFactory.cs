using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TbcaTest.Infra.Contexts;

namespace TbcaTest.Infra.IoC;

public class TbcaTestContextFactory : IDesignTimeDbContextFactory<TbcaTestContext>
{
    public TbcaTestContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TbcaTestContext>();

        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") 
                               ?? throw new ArgumentNullException("args");

        optionsBuilder.UseNpgsql(connectionString);
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        return new TbcaTestContext(optionsBuilder.Options);
    }
}

