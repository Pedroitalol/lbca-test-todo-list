using Microsoft.Extensions.DependencyInjection;
using TbcaTest.Application.Services;

namespace TbcaTest.Application.IoC;

public static class ApplicationIoC
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<TokenService>();
        services.AddScoped<StripeWebhookService>();
        
        return services;
    }
}


