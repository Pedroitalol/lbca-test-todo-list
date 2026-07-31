namespace TbcaTest.Application.DTOs.Auth;

public sealed record VerifiedFirebaseToken(
    string Uid,
    string Email,
    bool EmailVerified,
    string? Provider,
    string? Name,
    string? PictureUrl);


