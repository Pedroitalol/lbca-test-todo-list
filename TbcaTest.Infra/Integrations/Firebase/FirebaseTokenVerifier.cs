using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TbcaTest.Application.Abstractions.Integrations;
using TbcaTest.Application.DTOs.Auth;
using TbcaTest.CrossCutting.Configuration;

namespace TbcaTest.Infra.Integrations.Firebase;

public class FirebaseTokenVerifier(
    IOptions<FirebaseOptions> firebaseOptions,
    ILogger<FirebaseTokenVerifier> logger) : IFirebaseTokenVerifier
{
    private readonly FirebaseOptions _firebaseOptions = firebaseOptions.Value;
    private readonly object _sync = new();
    private FirebaseAuth? _firebaseAuth;

    public async Task<VerifiedFirebaseToken?> VerifyIdTokenAsync(
        string idToken,
        bool checkRevoked = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var decodedToken = await GetFirebaseAuth().VerifyIdTokenAsync(idToken, checkRevoked, cancellationToken);
            var provider = decodedToken.Claims.TryGetValue("firebase", out var firebaseClaim)
                ? ResolveProvider(firebaseClaim)
                : null;

            return new VerifiedFirebaseToken(
                decodedToken.Uid,
                decodedToken.Claims.GetValueOrDefault("email")?.ToString() ?? string.Empty,
                decodedToken.Claims.TryGetValue("email_verified", out var emailVerified) && emailVerified is true,
                provider,
                decodedToken.Claims.GetValueOrDefault("name")?.ToString(),
                decodedToken.Claims.GetValueOrDefault("picture")?.ToString());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Firebase token validation failed.");
            return null;
        }
    }

    private FirebaseAuth GetFirebaseAuth()
    {
        if (_firebaseAuth is not null)
        {
            return _firebaseAuth;
        }

        lock (_sync)
        {
            if (_firebaseAuth is not null)
            {
                return _firebaseAuth;
            }

            var app = FirebaseApp.DefaultInstance ?? CreateFirebaseApp();
            _firebaseAuth = FirebaseAuth.GetAuth(app);
            return _firebaseAuth;
        }
    }

    private FirebaseApp CreateFirebaseApp()
    {
        var options = new AppOptions
        {
            Credential = ResolveCredential()
        };

        if (!string.IsNullOrWhiteSpace(_firebaseOptions.ProjectId))
        {
            options.ProjectId = _firebaseOptions.ProjectId;
        }

        logger.LogInformation("Initializing Firebase Admin SDK. projectId={ProjectId}",
            string.IsNullOrWhiteSpace(options.ProjectId) ? "(default)" : options.ProjectId);

        return FirebaseApp.Create(options);
    }

    private GoogleCredential ResolveCredential()
    {
        var credentialsPath = string.IsNullOrWhiteSpace(_firebaseOptions.CredentialsPath)
            ? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")
            : _firebaseOptions.CredentialsPath;

        return string.IsNullOrWhiteSpace(credentialsPath)
            ? GoogleCredential.GetApplicationDefault()
            : GoogleCredential.FromFile(credentialsPath);
    }

    private static string? ResolveProvider(object firebaseClaim)
    {
        if (firebaseClaim is IReadOnlyDictionary<string, object> dictionary &&
            dictionary.TryGetValue("sign_in_provider", out var provider))
        {
            return provider?.ToString() == "google.com" ? "Google" : provider?.ToString();
        }

        return null;
    }
}


