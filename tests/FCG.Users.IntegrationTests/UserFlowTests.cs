using FCG.Users.Domain.Entity;
using FCG.Users.Domain.Interfaces;
using FCG.Users.Domain.Exceptions;
using FCG.Users.Domain.Models.User;
using FCG.Users.Domain.Services;
using FCG.Users.Infrastructure.Repository;

namespace FCG.Users.IntegrationTests;

/// <summary>
/// Cadastro e autenticação de usuário contra o banco real.
/// </summary>
public class UserFlowTests : IntegrationTestBase
{
    private readonly UserService _service;

    public UserFlowTests()
    {
        _service = new UserService(new UserRepository(Context), new FakeEventPublisher());
    }

    [Fact]
    public async Task DevePersistirOUsuario_QuandoDadosSaoValidos()
    {
        // Act
        var criado = await _service.CreateAsync(new RegisterUserRequest
        {
            Name = "Gabriela",
            Email = "gabriela@email.com",
            Login = "gabriela",
            Password = "Senha@123"
        });

        LimparRastreamento();

        // Assert
        var doBanco = await Context.Users.FindAsync(criado.Id);

        Assert.NotNull(doBanco);
        Assert.Equal("gabriela", doBanco.Login);
        Assert.Equal(PerfilEnum.Client, doBanco.Perfil);
    }

    [Fact]
    public async Task NuncaDeveGravarASenhaEmTextoPuro()
    {
        // Act
        await _service.CreateAsync(new RegisterUserRequest
        {
            Name = "Gabriela",
            Email = "gabriela@email.com",
            Login = "gabriela",
            Password = "Senha@123"
        });

        LimparRastreamento();

        // Assert
        var doBanco = Context.Users.Single();

        Assert.NotEqual("Senha@123", doBanco.Password);
        Assert.StartsWith("$2", doBanco.Password); // prefixo de hash BCrypt
    }

    [Fact]
    public async Task DeveLancarConflito_QuandoLoginJaExiste()
    {
        // Arrange
        await DadoUmUsuarioAsync(login: "gabriela", email: "gabriela@email.com");

        LimparRastreamento();

        // Act + Assert
        var excecao = await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync(new RegisterUserRequest
            {
                Name = "Outra",
                Email = "outra@email.com",
                Login = "gabriela",
                Password = "Senha@123"
            }));

        Assert.Equal("Login já cadastrado.", excecao.Message);

        LimparRastreamento();

        Assert.Single(Context.Users);
    }

    [Fact]
    public async Task DeveLancarConflito_QuandoEmailJaExiste()
    {
        // Arrange
        await DadoUmUsuarioAsync(login: "gabriela", email: "gabriela@email.com");

        LimparRastreamento();

        // Act + Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync(new RegisterUserRequest
            {
                Name = "Outra",
                Email = "gabriela@email.com",
                Login = "outro-login",
                Password = "Senha@123"
            }));

        LimparRastreamento();

        Assert.Single(Context.Users);
    }

    [Fact]
    public async Task DeveAutenticar_QuandoSenhaConfereComOHashGravado()
    {
        // Arrange
        await _service.CreateAsync(new RegisterUserRequest
        {
            Name = "Gabriela",
            Email = "gabriela@email.com",
            Login = "gabriela",
            Password = "Senha@123"
        });

        LimparRastreamento();

        // Act
        var autenticado = await _service.AuthenticateAsync("gabriela", "Senha@123");

        // Assert
        // Prova o ciclo completo: a senha foi hasheada na gravação e o BCrypt
        // conseguiu validá-la de volta a partir do que está no banco.
        Assert.Equal("gabriela", autenticado.Login);
    }

    [Fact]
    public async Task DeveLancarUnauthorized_QuandoSenhaEstaErrada()
    {
        // Arrange
        await DadoUmUsuarioAsync(login: "gabriela", senha: "Senha@123");

        LimparRastreamento();

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.AuthenticateAsync("gabriela", "SenhaErrada@1"));
    }

    [Fact]
    public async Task DeveLancarUnauthorized_QuandoLoginNaoExiste()
    {
        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.AuthenticateAsync("nao-existe", "Senha@123"));
    }

    [Fact]
    public async Task DeveRemoverOUsuarioDoBanco()
    {
        // Arrange
        var user = await DadoUmUsuarioAsync();

        LimparRastreamento();

        // Act
        await _service.DeleteAsync(user.Id);

        LimparRastreamento();

        // Assert
        Assert.Empty(Context.Users);
    }

    [Fact]
    public async Task DeveLancarNotFound_AoRemoverUsuarioInexistente()
    {
        // Act + Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(999));
    }

    [Fact]
    public async Task NaoDeveExporASenha_AoConsultarUsuario()
    {
        // Arrange
        var user = await DadoUmUsuarioAsync();

        LimparRastreamento();

        // Act
        var resposta = await _service.GetByIdAsync(user.Id);

        // Assert
        // UserResponse não tem campo de senha: o hash não pode vazar pela API.
        Assert.Equal("gabriela", resposta.Login);
        Assert.DoesNotContain(
            nameof(User.Password),
            resposta.GetType().GetProperties().Select(propriedade => propriedade.Name));
    }
}

/// <summary>
/// Substituto do broker nos testes de integração: guarda o que foi publicado,
/// sem precisar do RabbitMQ no ar.
/// </summary>
public class FakeEventPublisher : IEventPublisher
{
    public List<object> Published { get; } = new();

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        Published.Add(@event);
        return Task.CompletedTask;
    }
}
