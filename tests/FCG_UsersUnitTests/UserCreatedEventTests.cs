using FCG_UsersDomain.Events;
using FCG_UsersDomain.Entity;
using FCG_UsersDomain.Exceptions;
using FCG_UsersDomain.Interfaces;
using FCG_UsersDomain.Models.User;
using FCG_UsersDomain.Services;
using Moq;

namespace FCG_UsersUnitTests;

/// <summary>
/// Fluxo de cadastro: o UsersAPI precisa publicar o UserCreatedEvent
/// (e somente quando o usuário foi realmente criado).
/// </summary>
public class UserCreatedEventTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly UserService _service;

    public UserCreatedEventTests()
    {
        _repository.Setup(r => r.GetByLoginAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _repository.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _repository.Setup(r => r.AddAsync(It.IsAny<User>())).ReturnsAsync((User u) => { u.Id = 42; return u; });

        _service = new UserService(_repository.Object, _publisher.Object);
    }

    [Fact]
    public async Task DevePublicarUserCreatedEvent_QuandoUsuarioECriado()
    {
        await _service.CreateAsync(new RegisterUserRequest
        {
            Name = "Gabriela",
            Email = "gabriela@email.com",
            Login = "gabriela",
            Password = "Senha@123"
        });

        _publisher.Verify(p => p.Publish(
            It.Is<UserCreatedEvent>(e =>
                e.UserId == 42 &&
                e.Name == "Gabriela" &&
                e.Email == "gabriela@email.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NaoDevePublicarEvento_QuandoLoginJaExiste()
    {
        _repository.Setup(r => r.GetByLoginAsync("gabriela"))
            .ReturnsAsync(new User("Outra", "outra@email.com", "gabriela", "Senha@123", PerfilEnum.Client));

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(new RegisterUserRequest
        {
            Name = "Gabriela",
            Email = "gabriela@email.com",
            Login = "gabriela",
            Password = "Senha@123"
        }));

        _publisher.Verify(p => p.Publish(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NuncaDeveIncluirSenhaNoEvento()
    {
        UserCreatedEvent? publicado = null;

        _publisher.Setup(p => p.Publish(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<UserCreatedEvent, CancellationToken>((e, _) => publicado = e)
            .Returns(Task.CompletedTask);

        await _service.CreateAsync(new RegisterUserRequest
        {
            Name = "Gabriela",
            Email = "gabriela@email.com",
            Login = "gabriela",
            Password = "Senha@123"
        });

        Assert.NotNull(publicado);
        Assert.DoesNotContain(typeof(UserCreatedEvent).GetProperties(), p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }
}
