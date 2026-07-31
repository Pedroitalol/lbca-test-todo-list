using Microsoft.Extensions.DependencyInjection;
using TbcaTest.Application.Services;

namespace TbcaTest.Application.IoC;

public static class ApplicationIoC
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IStripeWebhookService, StripeWebhookService>();
        services.AddScoped<ITaskService, TaskService>();
        
        return services;
    }
}


