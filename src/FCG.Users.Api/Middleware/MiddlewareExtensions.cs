using FCG.Users.Api.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using FCG.Users.Api.Configuration;

namespace FCG.Users.Api.Middleware;

public static class MiddlewareExtensions
{
    /// <summary>
    /// Registra o tratamento centralizado de exceções.
    ///
    /// A validação do token NÃO entra aqui: quem faz isso é o UseAuthentication(),
    /// configurado em AddJwtAuthentication(). Ele já confere assinatura, emissor,
    /// audiência e expiração, e devolve 401 sozinho quando o token não presta.
    /// </summary>
    public static WebApplication UseExceptionHandling(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        return app;
    }

    /// <summary>
    /// Monta o pipeline de requisições. A ordem aqui é o próprio comportamento da
    /// aplicação: cada middleware envolve todos os que vêm depois dele.
    /// </summary>
    public static WebApplication UseInfrastructurePipeline(this WebApplication app)
    {
        // O log de requisições vem primeiro para envolver tudo, inclusive o
        // tratamento de exceções. Invertido, ele veria a exceção antes de virar
        // 404/400/409 e registraria toda falha de negócio como erro 500 —
        // contradizendo o status que o cliente de fato recebeu.
        app.UseRequestLogging();
        app.UseExceptionHandling();

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "FCG Users API v1");
            c.RoutePrefix = string.Empty;
        });

        // Sem UseHttpsRedirection: dentro do container/cluster o tráfego é HTTP na porta
        // 8080 e o TLS, quando existir, termina no Ingress. Com o redirect ligado o
        // ASP.NET apenas registraria um aviso, sem redirecionar nada.

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        // Readiness: inclui o health check do bus do MassTransit (RabbitMQ).
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteAsync
        });

        // Liveness: só diz que o processo responde, sem depender do RabbitMQ.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        return app;
    }
}
