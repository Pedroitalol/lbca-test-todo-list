using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using TbcaTest.Application.Services;
using TbcaTest.CrossCutting.Configuration;
using TbcaTest.Domain.Entities;

namespace TbcaTest.Tests.Services;

public class TokenServiceTests
{
    [Fact]
    public void GenerateToken_includes_identity_claims_without_raw_email()
    {
        var service = new TokenService(Options.Create(new JwtOptions
        {
            Key = "test-secret-with-at-least-thirty-two-bytes",
            Issuer = "TbcaTest-api",
            Audience = "TbcaTest-api"
        }));
        var client = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Owner",
            Email = "owner@example.com",
            Plan = Plan.Standard,
            Role = Roles.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var token = service.GenerateToken(client);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(claim => claim.Type == "tenant_id" && claim.Value == client.Id.ToString());
        jwt.Claims.Should().NotContain(claim => claim.Value == client.Email);
    }
}


