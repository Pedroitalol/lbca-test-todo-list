namespace TbcaTest.CrossCutting.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "TbcaTest-api";
    public string Audience { get; set; } = "TbcaTest-api";
    public int ExpirationHours { get; set; } = 8;
}


