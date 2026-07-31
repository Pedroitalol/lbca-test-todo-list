using Microsoft.EntityFrameworkCore;
using TbcaTest.Domain.Entities;

namespace TbcaTest.Infra.Contexts;

public class TbcaTestContext(DbContextOptions<TbcaTestContext> options) : DbContext(options)
{
    public DbSet<Client> Client { get; set; }
    public DbSet<TaskItem> TaskItems { get; set; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.EnableDetailedErrors();
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TbcaTestContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}

