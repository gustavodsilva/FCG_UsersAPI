# FCG Users API

Microsserviço de **Usuários** da FIAP Cloud Games (FCG), Tech Challenge Fase 2.

## Finalidade

- Cadastro de usuários (`POST /api/auth/register`)
- Autenticação com geração de token JWT (`POST /api/auth/login`)
- Autorização por perfil (`Admin` e `Client`) e administração de usuários (`/api/users`, somente Admin)
- Publicação do evento `UserCreatedEvent` no RabbitMQ a cada cadastro

Catálogo, Pagamentos e Notificações ficam em repositórios próprios. O UsersAPI não chama nenhum deles: só publica eventos.

## Endpoints

| Método | Rota                  | Acesso  | Descrição                                              |
|--------|-----------------------|---------|--------------------------------------------------------|
| POST   | `/api/auth/register`  | Público | Cadastra usuário (perfil Client) e publica o evento    |
| POST   | `/api/auth/login`     | Público | Autentica e devolve o token JWT                        |
| GET    | `/api/users`          | Admin   | Lista os usuários                                      |
| GET    | `/api/users/{id}`     | Admin   | Consulta um usuário                                    |
| PUT    | `/api/users/{id}`     | Admin   | Atualiza um usuário                                    |
| DELETE | `/api/users/{id}`     | Admin   | Remove um usuário                                      |
| GET    | `/health`             | Público | Readiness (inclui a conexão com o RabbitMQ)            |
| GET    | `/health/live`        | Público | Liveness (o processo responde)                         |

## Fluxo orientado a eventos

```
UsersAPI --(UserCreatedEvent)--> RabbitMQ --> NotificationsAPI (e-mail de boas-vindas)
```

O evento é publicado na exchange `FCG_UserApi:user-created-event`, em **JSON puro** (sem o
envelope do MassTransit):

| Campo    | Tipo   | Descrição            |
|----------|--------|----------------------|
| `userId` | int    | Id do usuário criado |
| `name`   | string | Nome                 |
| `email`  | string | E-mail de destino    |

```json
{
  "userId": 7,
  "name": "Maria",
  "email": "maria@fcg.com"
}
```

A senha nunca vai no evento. Como consumir (exchange, fila e exemplo de código) está em
[docs/INTEGRACAO.md](docs/INTEGRACAO.md).

O evento é publicado **depois** de o usuário ser gravado. Se a publicação falhar (por exemplo,
RabbitMQ fora do ar), o erro é registrado em log e a requisição de cadastro responde `500`, embora o
usuário já tenha sido gravado: uma nova tentativa de cadastro devolve `409`. Uma melhoria futura é o
padrão Outbox.

## Estrutura

```
src/
  FCG_UsersApi             controllers, middlewares, health checks, configuração
  FCG_UsersDomain          entidades, regras de negócio, contrato do evento
  FCG_UsersInfrastructure  EF Core, repositórios, migrations, JWT, MassTransit/RabbitMQ
tests/
  FCG_UsersUnitTests       testes de unidade e cenários BDD
  FCG_UsersIntegrationTests
docs/                      guia de integração para quem consome o evento
Dockerfile                 multi-stage (restore, publish, runtime)
FCG_Users.sln              solução
```

O Docker Compose e os manifestos do Kubernetes ficam no repositório de orquestração
(veja a seção **Executar**).

## Variáveis de ambiente

No .NET, `__` (dois underscores) representa `:` na configuração. O `appsettings.json` da imagem
não traz credenciais: segredos e dados de conexão chegam por variáveis de ambiente.

| Variável                           | Sensível | Descrição                                    | Padrão                |
|------------------------------------|----------|----------------------------------------------|-----------------------|
| `ConnectionStrings__SqlConnection` | Sim      | Connection string do SQL Server              | nenhum (obrigatória)  |
| `Jwt__SecretKey`                   | Sim      | Chave de assinatura (mínimo 32 caracteres)   | nenhum (obrigatória)  |
| `Jwt__Issuer`                      | Não      | Emissor do token                             | `FCG.UsersAPI`        |
| `Jwt__Audience`                    | Não      | Audiência do token                           | `FCG.Platform`        |
| `Jwt__ExpirationInMinutes`         | Não      | Validade do token                            | `60`                  |
| `RabbitMq__Host`                   | Não      | Host do RabbitMQ                             | `localhost`           |
| `RabbitMq__Port`                   | Não      | Porta AMQP                                   | `5672`                |
| `RabbitMq__VirtualHost`            | Não      | Virtual host                                 | `/`                   |
| `RabbitMq__Username`               | Sim      | Usuário do broker                            | nenhum (obrigatória)  |
| `RabbitMq__Password`               | Sim      | Senha do broker                              | nenhum (obrigatória)  |

`Jwt__SecretKey`, `Jwt__Issuer` e `Jwt__Audience` precisam ser **iguais** nos serviços que validam
o token emitido por este. A API não sobe se a chave tiver menos de 32 caracteres.

## Executar

### Com a aplicação completa (recomendado)

O Docker Compose e os manifestos do Kubernetes estão no repositório de orquestração:
[FCG-Orchestration](https://github.com/pri-pereiras/FCG-Orchestration). Siga o README dele, que
sobe este serviço com o banco, o RabbitMQ e os demais serviços, já com as variáveis acima. No
cluster, o serviço fica acessível aos demais em `http://users-api:80`.

### Só este serviço, na sua máquina

1. Suba as dependências (troque `<senha>` e `<usuario>` pelos valores que preferir):

   ```bash
   docker run -d --name fcg-users-db \
     -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='<senha>' \
     -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest

   docker run -d --name fcg-rabbitmq \
     -e RABBITMQ_DEFAULT_USER='<usuario>' -e RABBITMQ_DEFAULT_PASS='<senha>' \
     -p 5672:5672 -p 15672:15672 rabbitmq:3.13-management
   ```

   A senha do SQL Server precisa ter 8+ caracteres, com maiúscula, minúscula, número e símbolo.

2. Crie `src/FCG_UsersApi/appsettings.Development.json` (ignorado pelo Git e pela imagem):

   ```json
   {
     "ConnectionStrings": {
       "SqlConnection": "Server=localhost,1433;Database=FcgUsersDb;User Id=sa;Password=<senha>;TrustServerCertificate=True;"
     },
     "Jwt": {
       "SecretKey": "<chave com 32 ou mais caracteres>"
     },
     "RabbitMq": {
       "Username": "<usuario>",
       "Password": "<senha>"
     }
   }
   ```

3. Rode a API:

   ```bash
   dotnet run --project src/FCG_UsersApi
   ```

As migrations e o seed são aplicados na largada. Painel do RabbitMQ: http://localhost:15672.

## Testar o fluxo de cadastro

Pelo Swagger (na raiz da porta em que a API estiver publicada):

1. `POST /api/auth/register` com nome, e-mail, login e senha. Esperado: `201`, com `perfil: 2` (Client).
2. `POST /api/auth/login`. Esperado: `200`, com o `token`.
3. No painel do RabbitMQ, em **Exchanges**, confira `FCG_UserApi:user-created-event`.

Para os endpoints protegidos, clique em **Authorize** e informe `Bearer {token}` (a palavra `Bearer`,
um espaço e o token). Para trocar de usuário, volte ao botão e cole o novo token. `GET /api/users`
exige perfil Admin: com token de Client retorna `403`.

O seed cria o administrador `admin` (senha `Admin@123`) e usuários de teste da equipe
(veja `DbInitializer`). São dados de desenvolvimento.

## Autenticação e perfis

O token JWT é emitido por este serviço e validado localmente pelos demais (assinatura, Issuer, Audience
e validade), sem consulta ao UsersAPI. O token carrega o id do usuário, e-mail, nome e perfil
(`Admin` ou `Client`).

As respostas de erro seguem o formato `ProblemDetails` (RFC 7807): `400` (dados inválidos), `401`
(token ausente, inválido ou expirado), `403` (perfil sem permissão), `404`, `409` (login ou e-mail já
cadastrado) e `500`.

## Testes

```bash
dotnet test
```

## Imagem Docker

Publicada no Docker Hub: [`gustavotianosilva/fcg-users-api`](https://hub.docker.com/r/gustavotianosilva/fcg-users-api).

Use sempre uma **tag de versão** (por exemplo `1.0.1`), e não o `latest`; as tags disponíveis estão
na página do repositório. A imagem roda sem root na porta `8080` e não contém credenciais.

```bash
docker build -t gustavotianosilva/fcg-users-api:<versao> .
```
