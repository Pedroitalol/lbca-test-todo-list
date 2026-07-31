using FluentResults;
using Microsoft.AspNetCore.Http;
using TbcaTest.Api.Controllers;
using TbcaTest.Api.Responses;
using TbcaTest.Application.DTOs.Auth;
using TbcaTest.Application.Services;
using TbcaTest.Tests.TestHelpers;

namespace TbcaTest.Tests.Controllers;

public class AuthControllerTests
{
    [Fact]
    public async Task GoogleLogin_returns_api_response_with_lgpd_metadata()
    {
        var repository = new InMemoryClientRepository();
        var verifier = new TestFirebaseTokenVerifier
        {
            Token = new VerifiedFirebaseToken("uid", "owner@example.com", true, "Google", "Owner", null)
        };
        var authService = AuthServiceFactory.Create(repository, verifier);
        var controller = new AuthController(authService)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-test" }
            }
        };

        var response = await controller.GoogleLogin(new GoogleLoginRequest { IdToken = "token" }, CancellationToken.None);

        var ok = response.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeAssignableTo<ApiResponse<LoginResponse>>().Subject;
        payload.Meta.TraceId.Should().Be("trace-test");
        payload.Meta.Lgpd.DataCategories.Should().Contain("authentication");
        payload.Data.EmailMasked.Should().Be("ow***@example.com");
    }

    private static class AuthServiceFactory
    {
        public static AuthService Create(InMemoryClientRepository repository, TestFirebaseTokenVerifier verifier)
            => new(
                repository,
                verifier,
                new TokenService(Microsoft.Extensions.Options.Options.Create(new TbcaTest.CrossCutting.Configuration.JwtOptions
                {
                    Key = "test-secret-with-at-least-thirty-two-bytes",
                    Issuer = "TbcaTest-api",
                    Audience = "TbcaTest-api"
                })),
                Microsoft.Extensions.Options.Options.Create(new TbcaTest.CrossCutting.Configuration.FirebaseOptions
                {
                    RequireVerifiedEmail = true
                }),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance);
    }
}


