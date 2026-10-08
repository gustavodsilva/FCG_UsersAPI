using FCG.Users.Domain.Interfaces;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Users.Infrastructure.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// Registra o MassTransit com RabbitMQ. O UsersAPI só publica eventos; não há
    /// consumers aqui, então nenhuma fila é criada por este serviço.
    /// O MassTransit também registra um health check do bus, que alimenta o /health.
    /// </summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(RabbitMqSettings.SecaoConfiguracao).Get<RabbitMqSettings>()
            ?? new RabbitMqSettings();

        services.AddMassTransit(bus =>
        {
            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(settings.Host, settings.Port, settings.VirtualHost, host =>
                {
                    host.Username(settings.Username);
                    host.Password(settings.Password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        return services;
    }
}
