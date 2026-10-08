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

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        try
        {
            await _publishEndpoint.Publish(@event, cancellationToken);

            _logger.LogInformation("Evento {EventType} publicado.", typeof(TEvent).Name);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao publicar o evento {EventType}.", typeof(TEvent).Name);
        }
    }
}
