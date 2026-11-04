# Integração com o UsersAPI

Este guia é para quem consome os eventos do UsersAPI (hoje, o NotificationsAPI) ou valida os
tokens que ele emite.

## O evento

O UsersAPI publica o `UserCreatedEvent` no RabbitMQ sempre que um usuário é cadastrado pela API
(`POST /api/auth/register`). Os usuários criados pelo seed inicial não geram evento.

| Item              | Valor                                                              |
|-------------------|--------------------------------------------------------------------|
| Exchange          | `FCG_UserApi:user-created-event` (tipo `fanout`)                   |
| Formato           | JSON puro, **sem** o envelope do MassTransit                       |
| Quem publica      | UsersAPI, depois de gravar o usuário                               |

Corpo da mensagem:

```json
{
  "userId": 7,
  "name": "Maria",
  "email": "maria@fcg.com"
}
```

| Campo    | Tipo   | Descrição         |
|----------|--------|-------------------|
| `userId` | int    | Id do usuário     |
| `name`   | string | Nome              |
| `email`  | string | E-mail de destino |

A senha nunca vai no evento.

## Regra de ouro: o nome da exchange

Quem consome precisa ligar a sua fila à exchange `FCG_UserApi:user-created-event`. O namespace da
classe no consumidor **não importa** para o roteamento, mas o nome da exchange precisa ser exatamente
esse. Se for diferente, o consumidor escuta outra exchange e nunca recebe nada, sem nenhum erro.

## Exemplo de consumer (MassTransit 8.x)

Exemplo de partida: valide no seu ambiente antes de depender dele. O UsersAPI usa o MassTransit 8.2.5.

```csharp
using MassTransit;

// Seu contrato: o namespace pode ser qualquer um; o EntityName aponta para a exchange do UsersAPI.
[EntityName("FCG_UserApi:user-created-event")]
public record UserCreatedEvent
{
    public int UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

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

        // O UsersAPI publica JSON puro, então o consumidor precisa aceitar esse formato.
        cfg.UseRawJsonDeserializer();

        cfg.ConfigureEndpoints(context);
    });
});
```

Pacote: `dotnet add package MassTransit.RabbitMQ --version 8.2.5.12`
(a v9 do MassTransit é paga; use a 8.x).

## Se não usar MassTransit

A mensagem é JSON puro. Qualquer cliente do RabbitMQ (por exemplo `RabbitMQ.Client`) consegue
consumir: crie uma fila, ligue-a à exchange `FCG_UserApi:user-created-event` e leia o corpo como o
JSON acima.

## Validar o token JWT (CatalogAPI, PaymentsAPI)

O token é emitido pelo UsersAPI e validado localmente por cada serviço, sem consultar o UsersAPI nem
o banco dele. Cada serviço confere a assinatura, o Issuer, a Audience e a validade.

| Configuração    | Valor                                                                    |
|-----------------|--------------------------------------------------------------------------|
| `Jwt:Issuer`    | `FCG.UsersAPI`                                                           |
| `Jwt:Audience`  | `FCG.Platform`                                                           |
| `Jwt:SecretKey` | A chave combinada pelo grupo (mínimo 32 caracteres), a mesma em todos os serviços |

Se algum desses valores for diferente do usado pelo UsersAPI, todos os tokens são recusados com `401`.

Dados dentro do token:

| Dado                         | Onde ler                                                       |
|------------------------------|----------------------------------------------------------------|
| Id do usuário                | claim `sub` ou `NameIdentifier` (`ClaimTypes.NameIdentifier`)  |
| Perfil (`Admin` ou `Client`) | claim de role (`[Authorize(Roles = "Admin")]`)                 |
| E-mail e nome                | claims `email` e `name`                                        |
| Validade                     | 60 minutos                                                     |

Use o id do token como `UserId`, e não um id vindo no corpo da requisição.

## Dados de conexão

| Item                    | Valor                                                                        |
|-------------------------|------------------------------------------------------------------------------|
| Host do RabbitMQ        | `rabbitmq` (na orquestração, Compose e Kubernetes)                           |
| Porta                   | `5672`                                                                       |
| Virtual host            | `/`                                                                          |
| Usuário e senha         | Definidos no `.env` ou no Secret da orquestração, combinados pelo grupo       |
| Painel de administração | porta `15672` do RabbitMQ                                                    |

## Atenção ao testar

Se ninguém tiver uma fila ligada na exchange no momento da publicação, a mensagem se perde. Suba o
consumidor antes de cadastrar o usuário. O MassTransit cria a fila e a liga à exchange quando o
consumer sobe (`ConfigureEndpoints`). Depois que a fila existe, ela guarda as mensagens enquanto o
consumidor estiver fora do ar.