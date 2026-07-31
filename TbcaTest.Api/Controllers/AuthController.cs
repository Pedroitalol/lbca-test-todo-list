using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TbcaTest.Api.Responses;
using TbcaTest.Application.DTOs.Auth;
using TbcaTest.Application.Services;

namespace TbcaTest.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.GoogleLoginAsync(request, cancellationToken);
        return result.IsFailed
            ? ApiResponseFactory.BadRequest(HttpContext, result, "Authenticate user with Google Firebase token.", "account", "authentication")
            : ApiResponseFactory.Ok(HttpContext, result.Value, "Authenticate user with Google Firebase token.", "account", "authentication");
    }

    [AllowAnonymous]
    [HttpPost("firebase/validate")]
    public async Task<IActionResult> ValidateFirebaseToken(
        [FromBody] FirebaseTokenValidationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.ValidateFirebaseTokenAsync(request, cancellationToken);
        return result.IsFailed
            ? ApiResponseFactory.BadRequest(HttpContext, result, "Validate Firebase identity token.", "authentication")
            : ApiResponseFactory.Ok(HttpContext, result.Value, "Validate Firebase identity token.", "authentication");
    }
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return result.IsFailed
            ? ApiResponseFactory.BadRequest(HttpContext, result, "Register new user account.", "account", "authentication")
            : ApiResponseFactory.Ok(HttpContext, result.Value, "Register new user account.", "account", "authentication");
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result.IsFailed
            ? ApiResponseFactory.BadRequest(HttpContext, result, "Authenticate user with email and password.", "account", "authentication")
            : ApiResponseFactory.Ok(HttpContext, result.Value, "Authenticate user with email and password.", "account", "authentication");
    }
}


