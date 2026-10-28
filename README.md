# FCG Users API

Microsserviço de **Usuários** da FIAP Cloud Games (FCG), Tech Challenge Fase 2.

## Finalidade

- Cadastro de usuários (`POST /api/auth/register`)
- Autenticação com geração de token JWT (`POST /api/auth/login`)
- Autorização por perfil (`Admin` e `Client`) e administração de usuários (`/api/users`, somente Admin)
- Publicação do evento `UserCreatedEvent` no RabbitMQ a cada cadastro

Catálogo, Pagamentos e Notificações ficam em repositórios próprios. O UsersAPI não chama nenhum deles: só publica eventos.

## Fluxo orientado a eventos

```
UsersAPI --(UserCreatedEvent)--> RabbitMQ --> NotificationsAPI (e-mail de boas-vindas)
```

Contrato do evento (namespace `FCG.Contracts.Events`):

| Campo     | Tipo     | Descrição            |
|-----------|----------|----------------------|
| UserId    | int      | Id do usuário criado |
| Name      | string   | Nome                 |
| Email     | string   | E-mail de destino    |
| CreatedAt | DateTime | Data de criação      |

A senha nunca vai no evento. O consumidor deve declarar o evento com o mesmo namespace e nome
(`FCG.Contracts.Events.UserCreatedEvent`): o MassTransit nomeia a exchange a partir deles, e se
divergirem a mensagem não chega. Detalhes e exemplo de consumer em [docs/INTEGRACAO.md](docs/INTEGRACAO.md).

## Estrutura

```
src/
  FCG.Users.Api             controllers, middlewares, health checks, configuração
  FCG.Users.Domain          entidades, regras de negócio, contrato do evento
  FCG.Users.Infrastructure  EF Core, repositórios, migrations, JWT, MassTransit/RabbitMQ
tests/
  FCG.Users.UnitTests       testes de unidade e cenários BDD
  FCG.Users.IntegrationTests
docs/                       guia de integração para quem consome o evento
k8s/                        manifestos de referência (os oficiais ficam na orquestração)
Dockerfile                  multi-stage (restore, publish, runtime)
docker-compose.yml          UsersAPI + SQL Server + RabbitMQ, para rodar isolado
.env.example                modelo das variáveis secretas do Compose
```

## Variáveis de ambiente da aplicação

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
o token emitido por este.

## Executar com Docker Compose

O Compose lê os segredos do arquivo `.env`, que não vai para o Git.

1. Crie o `.env` a partir do modelo:

   ```bash
   cp .env.example .env
   ```

2. Preencha as quatro variáveis:

   | Variável             | Descrição                                          |
   |----------------------|----------------------------------------------------|
   | `MSSQL_SA_PASSWORD`  | Senha do SQL Server (8+ caracteres, com maiúscula, minúscula, número e símbolo) |
   | `JWT_SECRET_KEY`     | Chave do JWT (32+ caracteres, igual à dos outros serviços) |
   | `RABBITMQ_USER`      | Usuário do RabbitMQ                                |
   | `RABBITMQ_PASS`      | Senha do RabbitMQ                                  |

3. Suba tudo:

   ```bash
   docker compose up -d --build
   docker compose ps
   ```

4. Acesse:
   - Swagger: http://localhost:8080
   - Painel do RabbitMQ: http://localhost:15672 (usuário e senha do `.env`)

O SQL Server guarda a senha dentro do volume. Se você trocar o `MSSQL_SA_PASSWORD` depois da primeira
execução, rode `docker compose down -v` antes de subir de novo (isso apaga os dados do banco).

## Testar o fluxo de cadastro

Pelo Swagger (http://localhost:8080):

1. `POST /api/auth/register` com nome, e-mail, login e senha. Esperado: `201`, com `perfil: 2` (Client).
2. `POST /api/auth/login`. Esperado: `200`, com o `token`.
3. No painel do RabbitMQ, em **Exchanges**, confira `FCG.Contracts.Events:UserCreatedEvent`.

Para os endpoints protegidos, clique em **Authorize** e informe `Bearer {token}` (com a palavra
`Bearer`, um espaço e o token). `GET /api/users` exige perfil Admin: com token de Client retorna `403`.

O seed cria um administrador para desenvolvimento: login `admin`, senha `Admin@123`.

## Autenticação e perfis

O token JWT é emitido por este serviço e validado localmente pelos demais (assinatura, Issuer, Audience
e validade), sem consulta ao UsersAPI. O token carrega o id do usuário, e-mail, nome e perfil
(`Admin` ou `Client`).

## Executar a API fora do Docker

Suba só as dependências e rode a API:

```bash
docker compose up -d users-db rabbitmq
dotnet run --project src/FCG.Users.Api
```

Crie `src/FCG.Users.Api/appsettings.Development.json` (ignorado pelo Git e pela imagem) com a
connection string, a chave do JWT e o usuário e a senha do RabbitMQ, usando os mesmos valores do seu `.env`.

## Testes

```bash
dotnet test
```

## Imagem Docker

Publicada no Docker Hub: `gustavotianosilva/fcg-users-api`.

| Tag      | Uso                                                        |
|----------|------------------------------------------------------------|
| `1.0.0`  | Versão fixa, a que a orquestração deve referenciar        |
| `latest` | Última publicada                                           |

A imagem roda sem root na porta `8080` e não contém credenciais.

## Kubernetes

Os manifestos oficiais (Deployment, Service, ConfigMap e Secret) ficam no repositório de orquestração
(FCG-Orchestration). A pasta `k8s/` deste repositório é uma versão de referência e pode estar
desatualizada em relação a ele. No cluster, o serviço fica acessível aos demais em `http://users-api:80`.

## Health checks

- `GET /health`: readiness, inclui a conexão com o RabbitMQ
- `GET /health/live`: liveness, só indica que o processo responde