using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FCG.Users.Infrastructure.Repository.Configuration;
using FCG.Users.Infrastructure.Repository;

namespace FCG.Users.Api.Extensions;

/// <summary>
/// Prepara o banco de dados antes de a aplicação começar a atender requisições.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Aplica as migrations pendentes e garante o administrador inicial.
    ///
    /// Se qualquer uma das duas etapas falhar, a exceção sobe e derruba a
    /// inicialização de propósito: uma aplicação que atende requisições com o banco
    /// ausente ou desatualizado responderia erro 500 em todos os endpoints,
    /// enquanto o container aparentaria estar saudável.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseExtensions");

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        logger.LogInformation("Aplicando migrations pendentes...");

        await context.Database.MigrateAsync();

        await DbInitializer.InitializeAsync(context);

        logger.LogInformation("Banco de dados pronto.");
    }
}
