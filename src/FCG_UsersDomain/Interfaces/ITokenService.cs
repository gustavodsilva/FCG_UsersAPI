using FCG_UsersDomain.Entity;

namespace FCG_UsersDomain.Interfaces;

public record GeneratedToken(string Token, DateTime ExpiraEm);
public interface ITokenService
{
    GeneratedToken GenerateToken(User usuario);
}
