namespace TbcaTest.Application.DTOs.Auth;

public sealed class GoogleLoginRequest
{
    public string? IdToken { get; set; }
    public string? FirebaseToken { get; set; }
    public string? Name { get; set; }

    public string? GetEffectiveIdToken()
        => string.IsNullOrWhiteSpace(IdToken) ? FirebaseToken : IdToken;
}


