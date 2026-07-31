using TbcaTest.Application.DTOs.Auth;

namespace TbcaTest.Application.Abstractions.Integrations;

public interface IFirebaseTokenVerifier
{
    Task<VerifiedFirebaseToken?> VerifyIdTokenAsync(
        string idToken,
        bool checkRevoked = false,
        CancellationToken cancellationToken = default);
}


