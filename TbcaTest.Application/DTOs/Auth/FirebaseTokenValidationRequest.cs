namespace TbcaTest.Application.DTOs.Auth;

public sealed class FirebaseTokenValidationRequest
{
    public string? IdToken { get; set; }
    public bool? CheckRevoked { get; set; }
}


