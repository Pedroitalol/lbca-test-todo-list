using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using TbcaTest.Infra.Contexts;

namespace TbcaTest.Infra.IoC;

public class TbcaTestContextFactory : IDesignTimeDbContextFactory<TbcaTestContext>
{
    public TbcaTestContext CreateDbContext(string[] args)
    {
        // Path to the API project where appsettings.json resides
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../TbcaTest.Api");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
                               ?? throw new InvalidOperationException("Connection string not found. Please ensure appsettings.Development.json exists in TbcaTest.Api or the CONNECTION_STRING environment variable is set.");

        var optionsBuilder = new DbContextOptionsBuilder<TbcaTestContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new TbcaTestContext(optionsBuilder.Options);
    }
}

