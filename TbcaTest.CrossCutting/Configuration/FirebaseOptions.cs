namespace TbcaTest.CrossCutting.Configuration;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string ProjectId { get; set; } = string.Empty;
    public string CredentialsPath { get; set; } = string.Empty;
    public bool RequireVerifiedEmail { get; set; } = true;
    public bool CheckRevokedIdTokens { get; set; }
}


