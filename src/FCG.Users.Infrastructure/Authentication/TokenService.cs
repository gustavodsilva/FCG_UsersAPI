using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FCG.Users.Domain.Entity;
using FCG.Users.Domain.Interfaces;

namespace FCG.Users.Infrastructure.Authentication;

/// <summary>
/// Gera o token JWT do usuário autenticado.
///
/// O token tem três partes (RFC 7519): header com o algoritmo, payload com as
/// claims e signature. As claims levam quem é o usuário e o seu perfil — é o
/// perfil que o [Authorize(Roles = "Admin")] lê para autorizar cada endpoint.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public TokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public GeneratedToken GenerateToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.Now.AddMinutes(_settings.ExpirationInMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Name),

            // Unique token identifier, useful for tracking and revocation.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            // This claim enables role-based authorization on endpoints.
            new(ClaimTypes.Role, user.Perfil.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString())

        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new GeneratedToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt
        );
    }
}