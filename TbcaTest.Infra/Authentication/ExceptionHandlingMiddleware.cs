using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TbcaTest.Infra.Authentication;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (TbcaTest.Domain.Exceptions.DomainValidationException ex)
        {
            logger.LogWarning(ex, "Domain validation failed.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                errors = new[] { ex.Message }
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled application error");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                errors = new[] { "Internal server error" },
                meta = new
                {
                    traceId = context.TraceIdentifier,
                    dataClassification = "operational",
                    lgpd = new
                    {
                        legalBasis = "legitimate-interest",
                        purpose = "Operate and secure the API.",
                        retention = "Retain only while needed for audit, security and legal obligations.",
                        dataCategories = new[] { "operational" }
                    }
                }
            }));
        }
    }
}


