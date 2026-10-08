namespace FCG.Users.Infrastructure.Messaging;

/// <summary>
/// Configuração do broker, lida da seção "RabbitMq". No Kubernetes, host/porta/vhost
/// vêm do ConfigMap e usuário/senha do Secret (RabbitMq__Username, RabbitMq__Password).
/// </summary>
public class RabbitMqSettings
{
    public const string SecaoConfiguracao = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public ushort Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}
