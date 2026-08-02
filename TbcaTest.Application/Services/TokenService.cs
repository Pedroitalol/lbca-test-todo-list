using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TbcaTest.CrossCutting.Configuration;
using TbcaTest.CrossCutting.Security;
using TbcaTest.Domain.Entities;

namespace TbcaTest.Application.Services;

public class TokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly SigningCredentials _signingCredentials;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public TokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
        var key = Encoding.UTF8.GetBytes(_jwtOptions.Key);
        _signingCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature);
    }

    public string GenerateToken(Client client)
    {
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
            SigningCredentials = _signingCredentials
        };
        
        var token = _tokenHandler.CreateToken(tokenDescriptor);
        
        return _tokenHandler.WriteToken(token);
    }
}


