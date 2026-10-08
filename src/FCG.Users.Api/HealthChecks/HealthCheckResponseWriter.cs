using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FCG.Users.Api.HealthChecks;

/// <summary>
/// Monta a resposta do endpoint /health em JSON.
///
/// Por padrão o ASP.NET Core devolve apenas o texto "Healthy". Aqui detalhamos
/// cada verificação registrada, para que seja possível saber qual delas falhou
/// quando o status geral vier como Unhealthy.
/// </summary>
public static class HealthCheckResponseWriter
{
    /// <summary>
    /// Chamado pelo ASP.NET Core a cada requisição ao endpoint de health check.
    /// </summary>
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var response = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = entry.Value.Duration.TotalMilliseconds
            })
        };

        return context.Response.WriteAsJsonAsync(response);
    }
}