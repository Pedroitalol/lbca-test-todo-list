namespace TbcaTest.Application.DTOs.Auth;

public sealed class LoginResponse
{
    public required string Token { get; init; }
    public required Guid ClientId { get; init; }
    public required string EmailMasked { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
}


