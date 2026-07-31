namespace TbcaTest.CrossCutting.Configuration;

public sealed class AppSecurityOptions
{
    public const string SectionName = "AppSecurity";

    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public int MaxRequestBodySizeBytes { get; set; } = 1_048_576;
    public RateLimitingOptions RateLimiting { get; set; } = new();
}

public sealed class RateLimitingOptions
{
    public int DefaultRequestsPerSecond { get; set; } = 20;
    public int AuthRequestsPerSecond { get; set; } = 5;
    public int WebhookRequestsPerSecond { get; set; } = 10;
}


