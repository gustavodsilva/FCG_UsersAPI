# FCG Users API

Microsserviço de **Usuários** da FIAP Cloud Games (FCG) — Tech Challenge, Fase 2.

## Finalidade

- Cadastro de usuários (`POST /api/auth/register`)
- Autenticação com geração de token JWT (`POST /api/auth/login`)
- Autorização por perfil (`Admin` e `Client`) e administração de usuários (`/api/users`, apenas Admin)
- Publica o evento `UserCreatedEvent` no RabbitMQ a cada cadastro

Os demais serviços (Catálogo, Pagamentos e Notificações) ficam em repositórios próprios.

## Fluxo orientado a eventos

```
UsersAPI --(UserCreatedEvent)--> RabbitMQ --> NotificationsAPI (e-mail de boas-vindas)
```

Contrato do evento (namespace `FCG.Contracts.Events`):

| Campo     | Tipo     | Descrição                    |
|-----------|----------|------------------------------|
| UserId    | int      | Id do usuário criado         |
| Name      | string   | Nome                         |
| Email     | string   | E-mail de destino            |
| CreatedAt | DateTime | Data de criação              |

Atenção: o NotificationsAPI deve declarar o evento com o **mesmo namespace e nome**
(`FCG.Contracts.Events.UserCreatedEvent`). O MassTransit nomeia a exchange a partir
deles; se divergirem, a mensagem não chega ao consumidor.

## Estrutura

```
src/
  FCG.Users.Api             controllers, middlewares, health checks, configuração
  FCG.Users.Domain          entidades, regras de negócio, contrato do evento
  FCG.Users.Infrastructure  EF Core, repositórios, migrations, MassTransit/RabbitMQ
tests/
  FCG.Users.UnitTests       testes de unidade e cenários BDD
  FCG.Users.IntegrationTests
k8s/                        Deployment, Service, ConfigMap, Secret (+ banco do serviço)
Dockerfile                  multi-stage (restore, publish, runtime)
docker-compose.yml          UsersAPI + SQL Server + RabbitMQ, para rodar isolado
```

## Variáveis de ambiente

No .NET, `__` (dois underscores) representa `:` na configuração.

| Variável                          | Sensível | Descrição                                   | Padrão (dev)              |
|-----------------------------------|----------|---------------------------------------------|---------------------------|
| `ConnectionStrings__SqlConnection`| Sim      | Connection string do SQL Server             | localhost, banco FcgUsersDb |
| `Jwt__SecretKey`                  | Sim      | Chave de assinatura (mínimo 32 caracteres)  | chave de desenvolvimento  |
| `Jwt__Issuer`                     | Não      | Emissor do token                            | `FCG.UsersAPI`            |
| `Jwt__Audience`                   | Não      | Audiência do token                          | `FCG.Platform`            |
| `Jwt__ExpirationInMinutes`        | Não      | Validade do token                           | `60`                      |
| `RabbitMq__Host`                  | Não      | Host do RabbitMQ                            | `localhost`               |
| `RabbitMq__Port`                  | Não      | Porta AMQP                                  | `5672`                    |
| `RabbitMq__VirtualHost`           | Não      | Virtual host                                | `/`                       |
| `RabbitMq__Username`              | Sim      | Usuário do broker                           | `guest`                   |
| `RabbitMq__Password`              | Sim      | Senha do broker                             | `guest`                   |
| `ASPNETCORE_ENVIRONMENT`          | Não      | Ambiente (Development / Production)         | `Production` no container |

`Jwt__SecretKey`, `Jwt__Issuer` e `Jwt__Audience` precisam ser **iguais** nos serviços que
validam o token emitido por este (Catálogo, Pagamentos).

## Executar com Docker Compose (este serviço isolado)

```bash
docker compose up -d --build
```

- Swagger: http://localhost:8080
- Painel do RabbitMQ: http://localhost:15672 (guest / guest)

Usuários criados na primeira execução: `admin` / `Admin@123` (Admin) e alguns clientes
(veja `DbInitializer`).

Teste o fluxo de cadastro:

```bash
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"name":"Maria","email":"maria@email.com","login":"maria","password":"Senha@123"}'
```

No painel do RabbitMQ, a exchange `FCG.Contracts.Events:UserCreatedEvent` passa a existir.

## Executar localmente (sem Docker para a API)

Suba apenas as dependências e rode a API:

```bash
docker compose up -d users-db rabbitmq
dotnet run --project src/FCG.Users.Api
```

## Testes

```bash
dotnet test
```

## Kubernetes

Pré-requisitos: `kubectl` e um cluster local (kind, minikube, k3d ou Docker Desktop).

O UsersAPI espera um Service chamado `rabbitmq`. Na aplicação completa ele vem do
repositório de orquestração. Para rodar este serviço isolado, use o RabbitMQ de
desenvolvimento em `k8s/dev/rabbitmq.yaml` (não aplique os dois ao mesmo tempo).

Exemplo com kind:

```bash
kind create cluster --name fcg
```

```bash
# 1. Construir a imagem
docker build -t fcg-users-api:latest .

# 2. Disponibilizar a imagem no cluster
kind load docker-image fcg-users-api:latest        # kind
# minikube image load fcg-users-api:latest         # minikube

# 3. Subir o RabbitMQ de desenvolvimento (somente ao rodar este serviço isolado)
kubectl apply -f k8s/dev/rabbitmq.yaml

# 4. Aplicar os manifestos do UsersAPI
kubectl apply -f k8s/

# 5. Conferir (aguarde 2 a 3 minutos: o SQL Server demora para ficar pronto)
kubectl get pods
kubectl get svc
```

Manifestos em `k8s/`:

| Arquivo           | Recurso                                                |
|-------------------|--------------------------------------------------------|
| `configmap.yaml`  | ConfigMap `users-api-config` (configurações não sensíveis) |
| `secret.yaml`     | Secret `users-api-secret` (connection string, JWT, broker) |
| `users-db.yaml`   | PVC, Deployment e Service do SQL Server do serviço     |
| `deployment.yaml` | Deployment `users-api` (2 réplicas, probes, resources) |
| `service.yaml`    | Service `users-api` (porta 80, acessível em `http://users-api:80`) |

Os valores do Secret são de desenvolvimento. Não use em ambiente real.

Health checks: `GET /health` (readiness, inclui o RabbitMQ) e `GET /health/live` (liveness).

### Testar no cluster

```bash
kubectl port-forward svc/users-api 8080:80
```

Em outro terminal:

```bash
curl -s http://localhost:8080/health

curl -s -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"name":"Joao","email":"joao@email.com","login":"joao","password":"Senha@123"}'

kubectl exec deploy/rabbitmq -- rabbitmqctl list_exchanges | grep UserCreatedEvent
```

Na primeira subida, uma das réplicas pode reiniciar uma vez: as duas tentam aplicar a
migration ao mesmo tempo, uma encerra e o Kubernetes a reinicia. É esperado e se resolve sozinho.
