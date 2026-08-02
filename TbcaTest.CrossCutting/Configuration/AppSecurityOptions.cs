namespace TbcaTest.CrossCutting.Configuration;

public sealed class AppSecurityOptions
{
    public const string SectionName = "AppSecurity";

    /// <summary>Global body size limit for all non-import endpoints (default: 1 MB).</summary>
    public long MaxRequestBodySizeBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Dedicated body size limit for the file import endpoint (default: 100 MB).
    /// Applied per-request via IHttpMaxRequestBodySizeFeature so the global limit stays tight.
    /// </summary>
    public long ImportMaxRequestBodySizeBytes { get; set; } = 104_857_600;

    /// <summary>
    /// Maximum number of row-level validation errors reported in the import response.
    /// Excess errors are suppressed and counted in TruncatedErrors (default: 500).
    /// </summary>
    public int ImportMaxReportedErrors { get; set; } = 500;

    public RateLimitingOptions RateLimiting { get; set; } = new();
}

public sealed class RateLimitingOptions
{
    public int DefaultRequestsPerSecond { get; set; } = 20;
    public int AuthRequestsPerSecond { get; set; } = 5;
    public int WebhookRequestsPerSecond { get; set; } = 10;
}


