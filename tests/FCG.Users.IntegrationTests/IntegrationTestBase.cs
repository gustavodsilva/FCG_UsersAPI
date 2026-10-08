using Microsoft.EntityFrameworkCore;
using FCG.Users.Domain.Entity;
using FCG.Users.Infrastructure.Repository;

namespace FCG.Users.IntegrationTests;

/// <summary>
/// Base dos testes de integração.
///
/// Diferente dos testes de unidade, aqui nada é mockado: o service conversa com o
/// repositório real, que conversa com o Entity Framework, que grava num banco de
/// verdade. O único substituto é o provider — banco em memória no lugar do SQL
/// Server — para o teste não depender do Docker estar ligado.
///
/// É essa conversa entre as camadas que o teste de integração precisa provar; que
/// cada camada funciona sozinha já é responsabilidade dos testes de unidade.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected ApplicationDbContext Context { get; }

    protected IntegrationTestBase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            // Nome único por teste: sem isso, um teste enxergaria os dados do outro.
            .UseInMemoryDatabase($"fcg-users-{Guid.NewGuid()}")
            .Options;

        Context = new ApplicationDbContext(options);
    }

    /// <summary>
    /// Grava um usuário e devolve a entidade já com o Id atribuído pelo banco.
    /// </summary>
    protected async Task<User> DadoUmUsuarioAsync(
        string login = "gabriela",
        string email = "gabriela@email.com",
        string senha = "Senha@123",
        PerfilEnum perfil = PerfilEnum.Client)
    {
        var user = new User("Gabriela", email, login, senha, perfil);

        Context.Users.Add(user);

        await Context.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Descarta o que o EF Core tem em memória, forçando as próximas consultas a
    /// irem ao banco. Sem isso o teste poderia passar lendo o objeto que ele mesmo
    /// acabou de criar, sem nunca provar que a gravação funcionou.
    /// </summary>
    protected void LimparRastreamento() => Context.ChangeTracker.Clear();

    public void Dispose()
    {
        Context.Dispose();

        GC.SuppressFinalize(this);
    }
}
