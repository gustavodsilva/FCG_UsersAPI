using FCG.Contracts.Events;
using FCG.Users.Domain.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FCG.Users.Infrastructure.Messaging;

/// <summary>
/// 
/// Publica eventos no RabbitMQ via MassTransit.
/// 
/// </summary>
public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MassTransitEventPublisher> _logger;

    public MassTransitEventPublisher(IPublishEndpoint publishEndpoint, ILogger<MassTransitEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Publish(UserCreatedEvent userCreatedEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(userCreatedEvent, cancellationToken);

            _logger.LogInformation("Evento {EventType} publicado.", typeof(UserCreatedEvent).Name);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao publicar o evento {EventType}.", typeof(UserCreatedEvent).Name);
        }
    }
}
