namespace FCG.Users.Domain.Interfaces;

/// <summary>
/// Porta para publicar eventos de domínio. O domínio só conhece esta interface;
/// quem fala com o RabbitMQ (MassTransit) é a camada de infraestrutura.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class;
}
