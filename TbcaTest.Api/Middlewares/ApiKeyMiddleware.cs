using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using TbcaTest.CrossCutting.Configuration;

namespace TbcaTest.Api.Middlewares;

public class ApiKeyMiddleware(RequestDelegate next, IOptions<AppSecurityOptions> securityOptions)
{
    private const string ApiKeyHeaderName = "X-API-KEY";
    private const string ApiSecretHeaderName = "X-API-SECRET";
    private readonly AppSecurityOptions _securityOptions = securityOptions.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method.Equals(HttpMethods.Options, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        if (IsPublicPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKey) ||
            !context.Request.Headers.TryGetValue(ApiSecretHeaderName, out var apiSecret) ||
            !CryptographicOperations.FixedTimeEquals(
                MemoryMarshal.AsBytes(apiKey.ToString().AsSpan()),
                MemoryMarshal.AsBytes(_securityOptions.ApiKey.AsSpan())) ||
            !CryptographicOperations.FixedTimeEquals(
                MemoryMarshal.AsBytes(apiSecret.ToString().AsSpan()),
                MemoryMarshal.AsBytes(_securityOptions.ApiSecret.AsSpan())))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(BuildErrorResponse(
                context,
                "Unauthorized",
                "Protect API access.")));
            return;
        }

        await next(context);
    }

    private static bool IsPublicPath(PathString path)
        => path.StartsWithSegments("/webhooks", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);

    private static object BuildErrorResponse(HttpContext context, string message, string purpose)
        => new
        {
            errors = new[] { message },
            meta = new
            {
                traceId = context.TraceIdentifier,
                dataClassification = "operational",
                lgpd = new
                {
                    legalBasis = "legitimate-interest",
                    purpose,
                    retention = "Retain only while needed for audit, security and legal obligations.",
                    dataCategories = new[] { "operational" }
                }
            }
        };
}


