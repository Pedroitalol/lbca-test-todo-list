using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TbcaTest.Application.Abstractions.Integrations;
using TbcaTest.Application.Abstractions.Persistence;
using TbcaTest.CrossCutting.Configuration;
using TbcaTest.Infra.Contexts;
using TbcaTest.Infra.Data.Repository;
using TbcaTest.Infra.Integrations.Firebase;

namespace TbcaTest.Infra.IoC;

public static class InfraIoC
{
    public static IServiceCollection AddInfra(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.Configure<FirebaseOptions>(configuration.GetSection(FirebaseOptions.SectionName));
        services.Configure<AppSecurityOptions>(configuration.GetSection(AppSecurityOptions.SectionName));
        services.Configure<DatabaseStartupOptions>(configuration.GetSection(DatabaseStartupOptions.SectionName));

        var connectionString = ResolveConnectionString(configuration);
        
        services.AddDbContext<TbcaTestContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly("TbcaTest.Infra");
                sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            }));

        services.AddScoped<DbContext, TbcaTestContext>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddSingleton<IFirebaseTokenVerifier, FirebaseTokenVerifier>();
        
        return services;
    }

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var directConnectionString = NormalizeConnectionString(Environment.GetEnvironmentVariable("CONNECTION_STRING"));
        if (directConnectionString is not null)
        {
            return directConnectionString;
        }



        var configuredConnectionString = NormalizeConnectionString(configuration.GetConnectionString("DefaultConnection"));
        if (configuredConnectionString is not null)
        {
            return configuredConnectionString;
        }

        throw new ArgumentException("Connection string not configured. Set CONNECTION_STRING, DATABASE_URL, or ConnectionStrings:DefaultConnection.");
    }



    private static string? NormalizeConnectionString(string? connectionString)
        => string.IsNullOrWhiteSpace(connectionString) ? null : connectionString.Trim();
}


