using FCG.Contracts.Events;

namespace FCG.Users.Domain.Interfaces;

/// <summary>
/// Porta para publicar eventos de domínio. O domínio só conhece esta interface;
/// quem fala com o RabbitMQ (MassTransit) é a camada de infraestrutura.
/// </summary>
public interface IEventPublisher
{
    Task Publish(UserCreatedEvent userCreatedEvent, CancellationToken cancellationToken = default);
}