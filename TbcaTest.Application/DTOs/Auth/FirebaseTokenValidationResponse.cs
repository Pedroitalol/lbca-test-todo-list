namespace TbcaTest.Application.DTOs.Auth;

public sealed class FirebaseTokenValidationResponse
{
    public required string Uid { get; init; }
    public required string EmailMasked { get; init; }
    public bool EmailVerified { get; init; }
    public string? Provider { get; init; }
}


