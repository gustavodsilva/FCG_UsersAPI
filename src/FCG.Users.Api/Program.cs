
using Serilog;
using FCG.Users.Api.Configuration;
using FCG.Users.Domain.Interfaces;
using FCG.Users.Infrastructure.Authentication;
using FCG.Users.Infrastructure.Messaging;
using FCG.Users.Api.Middleware;
using FCG.Users.Api.Extensions;

// Logger provisório, ativo apenas até o host subir com a configuração definitiva.
// Sem ele o Serilog usa um logger silencioso nesse intervalo, e uma falha antes do
// builder.Build() — connection string inválida, porta ocupada, migration quebrada —
// não seria registrada em lugar nenhum: o Log.Fatal lá embaixo escreveria no vazio.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilogLogging();

    // Mantém configuração do JWT disponível via IOptions<JwtSettings>
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SecaoConfiguracao));

    // Registrar serviços e documentação usando métodos centralizados
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);

    // RabbitMQ via MassTransit: publica o UserCreatedEvent no cadastro de usuário.
    builder.Services.AddMessaging(builder.Configuration);

    // /health (readiness) e /health/live (liveness) — usados pelas probes do Kubernetes.
    builder.Services.AddHealthChecks();
    builder.Services.AddSwaggerDocumentation();

    // Serviços da aplicação
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddControllers();

    var app = builder.Build();

    // Migração e seed do banco de dados
    await app.MigrateAndSeedAsync();

    // Pipeline de middlewares e roteamento
    app.UseInfrastructurePipeline();

    app.Run();
}

catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "A aplicação não pôde ser iniciada");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;