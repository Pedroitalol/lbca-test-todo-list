using TbcaTest.Domain.Entities;

namespace TbcaTest.Application.Services;

public interface ITokenService
{
    string GenerateToken(Client client);
}
