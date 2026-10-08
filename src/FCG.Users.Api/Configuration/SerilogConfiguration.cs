using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace FCG.Users.Api.Configuration;

/// <summary>
/// Configuração dos logs estruturados (Serilog).
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Em Development a saída é legível, para acompanhar durante o desenvolvimento.
    /// Em Production é JSON, onde cada propriedade do log vira um campo pesquisável.
    /// </summary>
    public static IHostBuilder UseSerilogLogging(this IHostBuilder host) =>
        host.UseSerilog((context, loggerConfiguration) =>
        {
            loggerConfiguration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                // "Now listening on: http://localhost:5022".
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "FCG.Users.Api")
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);

            if (context.HostingEnvironment.IsDevelopment())
            {
                loggerConfiguration.WriteTo.Console();
            }
            else
            {
                loggerConfiguration.WriteTo.Console(new CompactJsonFormatter());
            }
        });

    /// <summary>
    /// Grava uma linha por requisição HTTP com método, rota, status e duração.
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpContext, elapsed, exception) =>
            {
                if (exception is not null || httpContext.Response.StatusCode >= 500)
                {
                    return LogEventLevel.Error;
                }

                if (httpContext.Request.Path.StartsWithSegments("/health"))
                {
                    return LogEventLevel.Debug;
                }

                return LogEventLevel.Information;
            };
        });
}
