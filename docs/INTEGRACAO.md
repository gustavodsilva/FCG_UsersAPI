# Integração com o UsersAPI

Este guia é para quem consome os eventos do UsersAPI (hoje, o NotificationsAPI).

## O evento

O UsersAPI publica o `UserCreatedEvent` no RabbitMQ sempre que um usuário é
cadastrado pela API (`POST /api/auth/register`). Os usuários criados pelo seed
inicial não geram evento.

| Campo     | Tipo     | Descrição            |
|-----------|----------|----------------------|
| UserId    | int      | Id do usuário        |
| Name      | string   | Nome                 |
| Email     | string   | E-mail de destino    |
| CreatedAt | DateTime | Data de criação      |

A senha nunca vai no evento.

## Regra de ouro: mesmo namespace e mesmo nome

O MassTransit nomeia a exchange como `namespace:Tipo`. O consumidor precisa
declarar a classe com o namespace e o nome IDÊNTICOS aos do publicador:

    Exchange no RabbitMQ: FCG.Contracts.Events:UserCreatedEvent
    Classe:               FCG.Contracts.Events.UserCreatedEvent

Se o namespace for diferente, o consumidor escuta outra exchange e nunca recebe
nada, sem nenhum erro.

## Exemplo de consumer (MassTransit 8.x)

    // Contrato (copie igual)
    namespace FCG.Contracts.Events;

    public record UserCreatedEvent
    {
        public int UserId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }

    // Consumer
    public class UserCreatedConsumer : IConsumer<UserCreatedEvent>
    {
        private readonly ILogger<UserCreatedConsumer> _logger;

        public UserCreatedConsumer(ILogger<UserCreatedConsumer> logger)
            => _logger = logger;

        public Task Consume(ConsumeContext<UserCreatedEvent> context)
        {
            _logger.LogInformation(
                "E-mail de boas-vindas enviado para {Email} ({Name})",
                context.Message.Email, context.Message.Name);

            return Task.CompletedTask;
        }
    }

    // Program.cs
    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<UserCreatedConsumer>();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host("rabbitmq", "/", h =>
            {
                h.Username("guest");
                h.Password("guest");
            });

            cfg.ConfigureEndpoints(context);
        });
    });

Pacote: `dotnet add package MassTransit.RabbitMQ --version 8.3.6`
(a v9 do MassTransit é paga; use a 8.x).

## Se não usar MassTransit

O MassTransit publica um envelope JSON. Os dados do evento ficam no campo
`message`, e o tipo vai em `messageType`
(`urn:message:FCG.Contracts.Events:UserCreatedEvent`). Quem consumir com
`RabbitMQ.Client` precisa ler esse envelope e extrair o campo `message`.

## Dados de conexão

| Ambiente       | Host do RabbitMQ | Porta | Usuário / senha |
|----------------|------------------|-------|-----------------|
| Docker Compose | `rabbitmq`       | 5672  | guest / guest   |
| Kubernetes     | `rabbitmq`       | 5672  | guest / guest   |

Painel de administração (Compose): http://localhost:15672

## Atenção ao testar

Se ninguém tiver uma fila ligada na exchange no momento da publicação, a
mensagem se perde. Suba o consumidor ANTES de cadastrar o usuário.
Uma fila é criada e ligada à exchange automaticamente pelo MassTransit quando o
consumer sobe (`ConfigureEndpoints`).
