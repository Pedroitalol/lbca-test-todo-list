using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TbcaTest.Application.DTOs.Auth;
using TbcaTest.Application.Services;
using TbcaTest.CrossCutting.Configuration;
using TbcaTest.Domain.Entities;
using TbcaTest.Tests.TestHelpers;

namespace TbcaTest.Tests.Services;

public class AuthServiceTests
{
    [Fact]
    public async Task GoogleLoginAsync_creates_client_and_returns_lgpd_safe_response()
    {
        var repository = new InMemoryClientRepository();
        var verifier = new TestFirebaseTokenVerifier
        {
            Token = new VerifiedFirebaseToken(
                "firebase-uid",
                "Owner@Example.com",
                true,
                "Google",
                "Owner User",
                "https://example.com/photo.png")
        };
        var service = CreateService(repository, verifier);

        var result = await service.GoogleLoginAsync(new GoogleLoginRequest
        {
            FirebaseToken = "Bearer firebase-token"
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.EmailMasked.Should().Be("ow***@example.com");
        result.Value.Token.Should().NotBeNullOrWhiteSpace();
        verifier.IdTokenReceived.Should().Be("firebase-token");
        repository.Clients.Should().ContainSingle(client =>
            client.Email == "owner@example.com" &&
            client.FirebaseUid == "firebase-uid" &&
            client.AuthProvider == "Google" &&
            client.IsActive);
        repository.SaveChangesCalls.Should().Be(1);
    }

    [Fact]
    public async Task GoogleLoginAsync_rejects_non_google_provider()
    {
        var verifier = new TestFirebaseTokenVerifier
        {
            Token = new VerifiedFirebaseToken("uid", "user@example.com", true, "password", null, null)
        };
        var service = CreateService(new InMemoryClientRepository(), verifier);

        var result = await service.GoogleLoginAsync(new GoogleLoginRequest { IdToken = "token" });

        result.IsFailed.Should().BeTrue();
        result.Errors.Select(error => error.Message)
            .Should().Contain("The provided Firebase token is not from Google login.");
    }

    [Fact]
    public async Task ValidateFirebaseTokenAsync_returns_masked_email_only()
    {
        var verifier = new TestFirebaseTokenVerifier
        {
            Token = new VerifiedFirebaseToken("uid", "privacy@example.com", true, "Google", "Privacy", null)
        };
        var service = CreateService(new InMemoryClientRepository(), verifier);

        var result = await service.ValidateFirebaseTokenAsync(new FirebaseTokenValidationRequest
        {
            IdToken = "token",
            CheckRevoked = true
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.EmailMasked.Should().Be("pr***@example.com");
        verifier.CheckRevokedReceived.Should().BeTrue();
    }

    private static AuthService CreateService(
        InMemoryClientRepository repository,
        TestFirebaseTokenVerifier verifier)
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            Key = "test-secret-with-at-least-thirty-two-bytes",
            Issuer = "TbcaTest-api",
            Audience = "TbcaTest-api"
        });
        var firebaseOptions = Options.Create(new FirebaseOptions
        {
            RequireVerifiedEmail = true,
            CheckRevokedIdTokens = false
        });

        return new AuthService(
            repository,
            verifier,
            new TokenService(jwtOptions),
            firebaseOptions,
            NullLogger<AuthService>.Instance);
    }
}


