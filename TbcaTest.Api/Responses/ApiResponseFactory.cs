using FluentResults;
using Microsoft.AspNetCore.Mvc;

namespace TbcaTest.Api.Responses;

public static class ApiResponseFactory
{
    public static OkObjectResult Ok<T>(
        HttpContext context,
        T data,
        string purpose,
        params string[] dataCategories)
        => new(new ApiResponse<T>
        {
            Data = data,
            Meta = BuildMeta(context, purpose, dataCategories)
        });

    public static BadRequestObjectResult BadRequest<T>(
        HttpContext context,
        Result<T> result,
        string purpose,
        params string[] dataCategories)
        => new(new ApiErrorResponse
        {
            Errors = SanitizeErrors(result.Errors),
            Meta = BuildMeta(context, purpose, dataCategories)
        });

    public static ObjectResult StatusCode(
        HttpContext context,
        int statusCode,
        string message,
        string purpose,
        params string[] dataCategories)
        => new(new ApiErrorResponse
        {
            Errors = [message],
            Meta = BuildMeta(context, purpose, dataCategories)
        })
        {
            StatusCode = statusCode
        };

    private static ApiResponseMeta BuildMeta(HttpContext context, string purpose, string[] dataCategories)
        => new()
        {
            TraceId = context.TraceIdentifier,
            DataClassification = dataCategories.Length == 0 ? "operational" : "personal-data",
            Lgpd = new LgpdResponseMeta
            {
                LegalBasis = "contract-execution",
                Purpose = purpose,
                Retention = "Retain only while needed for the configured business purpose and legal obligations.",
                DataCategories = dataCategories.Length == 0 ? ["operational"] : dataCategories
            }
        };

    private static IReadOnlyCollection<string> SanitizeErrors(IReadOnlyCollection<IError> errors)
    {
        if (errors.Count == 0)
        {
            return ["The request could not be processed."];
        }

        return errors
            .Select(error => string.IsNullOrWhiteSpace(error.Message)
                ? "The request could not be processed."
                : error.Message)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}


