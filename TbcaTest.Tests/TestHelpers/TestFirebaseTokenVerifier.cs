using TbcaTest.Application.Abstractions.Integrations;
using TbcaTest.Application.DTOs.Auth;

namespace TbcaTest.Tests.TestHelpers;

public sealed class TestFirebaseTokenVerifier : IFirebaseTokenVerifier
{
    public VerifiedFirebaseToken? Token { get; set; }
    public bool CheckRevokedReceived { get; private set; }
    public string? IdTokenReceived { get; private set; }

    public Task<VerifiedFirebaseToken?> VerifyIdTokenAsync(
        string idToken,
        bool checkRevoked = false,
        CancellationToken cancellationToken = default)
    {
        IdTokenReceived = idToken;
        CheckRevokedReceived = checkRevoked;
        return Task.FromResult(Token);
    }
}


