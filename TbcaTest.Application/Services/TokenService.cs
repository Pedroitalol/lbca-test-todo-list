using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TbcaTest.CrossCutting.Configuration;
using TbcaTest.CrossCutting.Security;
using TbcaTest.Domain.Entities;

namespace TbcaTest.Application.Services;

public class TokenService(IOptions<JwtOptions> jwtOptions)
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public string GenerateToken(Client client)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtOptions.Key);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, client.Name),
                new Claim(ClaimTypes.Role, client.Role.ToString()),
                new Claim(ClaimTypes.NameIdentifier, client.Id.ToString()),
                new Claim(CustomClaims.TenantId, client.Id.ToString()),
            }),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            Expires = DateTime.UtcNow.AddHours(Math.Max(1, _jwtOptions.ExpirationHours)),
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return tokenHandler.WriteToken(token);
    }
}


