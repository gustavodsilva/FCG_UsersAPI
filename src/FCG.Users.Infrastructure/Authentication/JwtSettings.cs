namespace FCG.Users.Infrastructure.Authentication;

/// <summary>
/// Configurações do token JWT, lidas da seção "Jwt" do appsettings.
/// </summary>
public class JwtSettings
{
    public const string SecaoConfiguracao = "Jwt";

    /// <summary>Quem emite o token.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Quem consome o token.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Chave usada para assinar. Precisa ter no mínimo 32 caracteres: o algoritmo
    /// HMAC-SHA256 exige uma chave de 256 bits e recusa qualquer coisa menor.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    public int ExpirationInMinutes { get; set; } = 60;
}
