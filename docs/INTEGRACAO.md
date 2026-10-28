# Integração com o UsersAPI

Este guia é para quem consome os eventos do UsersAPI (hoje, o NotificationsAPI) ou valida os tokens que ele emite.

## O evento

O UsersAPI publica o `UserCreatedEvent` no RabbitMQ sempre que um usuário é cadastrado pela API
(`POST /api/auth/register`). Os usuários criados pelo seed inicial não geram evento.

| Campo     | Tipo     | Descrição         |
|-----------|----------|-------------------|
| UserId    | int      | Id do usuário     |
| Name      | string   | Nome              |
| Email     | string   | E-mail de destino |
| CreatedAt | DateTime | Data de criação   |

A senha nunca vai no evento.

## Regra de ouro: mesmo namespace e mesmo nome

O MassTransit nomeia a exchange como `namespace:Tipo`. O consumidor precisa declarar a classe com o
namespace e o nome idênticos aos do publicador:

```
Exchange no RabbitMQ: FCG.Contracts.Events:UserCreatedEvent
Classe:               FCG.Contracts.Events.UserCreatedEvent
```

Se o namespace for diferente, o consumidor escuta outra exchange e nunca recebe nada, sem nenhum erro.

## Exemplo de consumer (MassTransit 8.x)

```csharp
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

// Program.cs (host, usuário e senha vêm da configuração, nunca escritos no código)
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"]);
            h.Password(builder.Configuration["RabbitMq:Password"]);
        });

        cfg.ConfigureEndpoints(context);
    });
});
```

Pacote: `dotnet add package MassTransit.RabbitMQ --version 8.3.6`
(a v9 do MassTransit é paga; use a 8.x).

## Se não usar MassTransit

O MassTransit publica um envelope JSON. Os dados do evento ficam no campo `message`, e o tipo vai em
`messageType` (`urn:message:FCG.Contracts.Events:UserCreatedEvent`). Quem consumir com `RabbitMQ.Client`
precisa ler esse envelope e extrair o campo `message`.

## Validar o token JWT (CatalogAPI, PaymentsAPI)

O token é emitido pelo UsersAPI e validado localmente por cada serviço, sem consultar o UsersAPI nem o
banco dele. Cada serviço confere a assinatura, o Issuer, a Audience e a validade.

| Configuração    | Valor                                                    |
|-----------------|----------------------------------------------------------|
| `Jwt:Issuer`    | `FCG.UsersAPI`                                           |
| `Jwt:Audience`  | `FCG.Platform`                                           |
| `Jwt:SecretKey` | A chave combinada pelo grupo (mínimo 32 caracteres), a mesma em todos os serviços |

Se algum desses valores for diferente do usado pelo UsersAPI, todos os tokens são recusados com `401`.

Dados dentro do token:

| Dado                        | Onde ler                                          |
|-----------------------------|---------------------------------------------------|
| Id do usuário               | claim `NameIdentifier` (`ClaimTypes.NameIdentifier`) |
| Perfil (`Admin` ou `Client`) | claim de role (`[Authorize(Roles = "Admin")]`)    |
| E-mail e nome               | claims `email` e `name`                           |
| Validade                    | 60 minutos                                        |

Use o id do token como `UserId`, e não um id vindo no corpo da requisição.

## Dados de conexão

| Item                    | Valor                                              |
|-------------------------|----------------------------------------------------|
| Host do RabbitMQ        | `rabbitmq` (Docker Compose e Kubernetes)           |
| Porta                   | `5672`                                             |
| Virtual host            | `/`                                                |
| Usuário e senha         | Definidos no `.env` (Compose) ou no Secret (Kubernetes); combinados pelo grupo. Veja `.env.example` |
| Painel de administração | http://localhost:15672 (Compose)                   |

## Atenção ao testar

Se ninguém tiver uma fila ligada na exchange no momento da publicação, a mensagem se perde. Suba o
consumidor antes de cadastrar o usuário. O MassTransit cria a fila e a liga à exchange automaticamente
quando o consumer sobe (`ConfigureEndpoints`). Depois que a fila existe, ela guarda as mensagens
enquanto o consumidor estiver fora do ar.