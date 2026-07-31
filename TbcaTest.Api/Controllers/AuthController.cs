using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TbcaTest.Api.Responses;
using TbcaTest.Application.DTOs.Auth;
using TbcaTest.Application.Services;

namespace TbcaTest.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
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
}


