using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TbcaTest.Application.Abstractions.Integrations;
using TbcaTest.Application.Abstractions.Persistence;
using TbcaTest.Application.DTOs.Auth;
using TbcaTest.CrossCutting.Configuration;
using TbcaTest.CrossCutting.Security;
using TbcaTest.Domain.Entities;

namespace TbcaTest.Application.Services;

public class AuthService(
    IClientRepository clientRepository,
    IFirebaseTokenVerifier firebaseTokenVerifier,
    TokenService tokenService,
    IOptions<FirebaseOptions> firebaseOptions,
    ILogger<AuthService> logger)
{
    private const string GoogleProvider = "Google";
    private readonly FirebaseOptions _firebaseOptions = firebaseOptions.Value;

    public async Task<Result<LoginResponse>> GoogleLoginAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var idToken = NormalizeToken(request.GetEffectiveIdToken());
        if (idToken is null)
        {
            return Result.Fail<LoginResponse>("Firebase ID token is required.");
        }

        var firebaseToken = await firebaseTokenVerifier.VerifyIdTokenAsync(
            idToken,
            _firebaseOptions.CheckRevokedIdTokens,
            cancellationToken);

        if (firebaseToken is null)
        {
            logger.LogWarning("Google login rejected because Firebase token validation failed.");
            return Result.Fail<LoginResponse>("Invalid Firebase ID token.");
        }

        if (!string.Equals(firebaseToken.Provider, GoogleProvider, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<LoginResponse>("The provided Firebase token is not from Google login.");
        }

        var normalizedEmail = NormalizeEmail(firebaseToken.Email);
        if (normalizedEmail is null)
        {
            return Result.Fail<LoginResponse>("Google account email is required.");
        }

        if (_firebaseOptions.RequireVerifiedEmail && !firebaseToken.EmailVerified)
        {
            return Result.Fail<LoginResponse>("Google account email must be verified.");
        }

        var client = await clientRepository.GetByFirebaseUidAsync(firebaseToken.Uid, cancellationToken)
            ?? await clientRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (client is null)
        {
            client = new Client
            {
                Id = Guid.NewGuid(),
                Name = ResolveName(request.Name, firebaseToken.Name, normalizedEmail),
                Email = normalizedEmail,
                FirebaseUid = firebaseToken.Uid,
                AuthProvider = GoogleProvider,
                Plan = Plan.Standard,
                Role = Roles.Client,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await clientRepository.Create(client, cancellationToken);
        }
        else
        {
            client.FirebaseUid ??= firebaseToken.Uid;
            client.AuthProvider = GoogleProvider;
            client.Email = normalizedEmail;
            client.Name = string.IsNullOrWhiteSpace(client.Name)
                ? ResolveName(request.Name, firebaseToken.Name, normalizedEmail)
                : client.Name;
            client.UpdatedAt = DateTime.UtcNow;
            clientRepository.Update(client);
        }

        await clientRepository.SaveChanges(cancellationToken);
        logger.LogInformation("Google login succeeded. email={Email} clientId={ClientId}",
            PersonalDataMasker.MaskEmail(client.Email),
            client.Id);

        return Result.Ok(BuildLoginResponse(client));
    }

    public async Task<Result<FirebaseTokenValidationResponse>> ValidateFirebaseTokenAsync(
        FirebaseTokenValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var idToken = NormalizeToken(request.IdToken);
        if (idToken is null)
        {
            return Result.Fail<FirebaseTokenValidationResponse>("Firebase ID token is required.");
        }

        var token = await firebaseTokenVerifier.VerifyIdTokenAsync(
            idToken,
            request.CheckRevoked ?? _firebaseOptions.CheckRevokedIdTokens,
            cancellationToken);

        return token is null
            ? Result.Fail<FirebaseTokenValidationResponse>("Invalid Firebase ID token.")
            : Result.Ok(new FirebaseTokenValidationResponse
            {
                Uid = token.Uid,
                EmailMasked = PersonalDataMasker.MaskEmail(token.Email),
                EmailVerified = token.EmailVerified,
                Provider = token.Provider
            });
    }

    private LoginResponse BuildLoginResponse(Client client)
        => new()
        {
            Token = tokenService.GenerateToken(client),
            ClientId = client.Id,
            EmailMasked = PersonalDataMasker.MaskEmail(client.Email),
            Name = client.Name,
            Role = client.Role.ToString()
        };

    private static string ResolveName(string? requestName, string? tokenName, string email)
        => NormalizeOptional(requestName) ?? NormalizeOptional(tokenName) ?? email.Split('@')[0];

    private static string? NormalizeEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static string? NormalizeToken(string? token)
    {
        var value = NormalizeOptional(token);
        if (value is null)
        {
            return null;
        }

        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            value = value["Bearer ".Length..].Trim();
        }

        return value.Trim('"', '\'');
    }

    public async Task<Result<LoginResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        if (normalizedEmail is null)
        {
            return Result.Fail<LoginResponse>("Email is required.");
        }

        var existingClient = await clientRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingClient is not null)
        {
            return Result.Fail<LoginResponse>("An account with this email already exists.");
        }

        var client = new Client
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            AuthProvider = "Local",
            Plan = Plan.Standard,
            Role = Roles.Client,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await clientRepository.Create(client, cancellationToken);
        await clientRepository.SaveChanges(cancellationToken);
        
        logger.LogInformation("Local registration succeeded. email={Email} clientId={ClientId}",
            PersonalDataMasker.MaskEmail(client.Email),
            client.Id);

        return Result.Ok(BuildLoginResponse(client));
    }

    public async Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        if (normalizedEmail is null)
        {
            return Result.Fail<LoginResponse>("Email is required.");
        }

        var client = await clientRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (client is null || string.IsNullOrWhiteSpace(client.PasswordHash))
        {
            logger.LogWarning("Local login failed: Invalid email or password. email={Email}",
                PersonalDataMasker.MaskEmail(normalizedEmail));
            return Result.Fail<LoginResponse>("Invalid email or password.");
        }

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, client.PasswordHash);
        if (!isPasswordValid)
        {
            logger.LogWarning("Local login failed: Invalid email or password. email={Email}",
                PersonalDataMasker.MaskEmail(normalizedEmail));
            return Result.Fail<LoginResponse>("Invalid email or password.");
        }

        logger.LogInformation("Local login succeeded. email={Email} clientId={ClientId}",
            PersonalDataMasker.MaskEmail(client.Email),
            client.Id);

        return Result.Ok(BuildLoginResponse(client));
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}


