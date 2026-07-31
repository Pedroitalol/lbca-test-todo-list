namespace TbcaTest.Api.Responses;

public sealed class ApiResponse<T>
{
    public required T Data { get; init; }
    public required ApiResponseMeta Meta { get; init; }
}

public sealed class ApiErrorResponse
{
    public required IReadOnlyCollection<string> Errors { get; init; }
    public required ApiResponseMeta Meta { get; init; }
}

public sealed class ApiResponseMeta
{
    public required string TraceId { get; init; }
    public required string DataClassification { get; init; }
    public required LgpdResponseMeta Lgpd { get; init; }
}

public sealed class LgpdResponseMeta
{
    public required string LegalBasis { get; init; }
    public required string Purpose { get; init; }
    public required string Retention { get; init; }
    public required string[] DataCategories { get; init; }
}


