using FCG.Users.Domain.Entity;

namespace FCG.Users.Domain.Interfaces;

public record GeneratedToken(string Token, DateTime ExpiraEm);
public interface ITokenService
{
    GeneratedToken GenerateToken(User usuario);
}
