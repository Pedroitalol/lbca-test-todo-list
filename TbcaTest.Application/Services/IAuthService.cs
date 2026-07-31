using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using TbcaTest.Application.DTOs.Auth;

namespace TbcaTest.Application.Services;

public interface IAuthService
{
    Task<Result<LoginResponse>> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<FirebaseTokenValidationResponse>> ValidateFirebaseTokenAsync(FirebaseTokenValidationRequest request, CancellationToken cancellationToken = default);
    Task<Result<LoginResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
