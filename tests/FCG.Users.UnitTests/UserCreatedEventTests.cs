using FCG.Contracts.Events;
using FCG.Users.Domain.Entity;
using FCG.Users.Domain.Exceptions;
using FCG.Users.Domain.Interfaces;
using FCG.Users.Domain.Models.User;
using FCG.Users.Domain.Services;
using Moq;

namespace FCG.Users.UnitTests;

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

        _publisher.Verify(p => p.PublishAsync(
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

        _publisher.Verify(p => p.PublishAsync(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NuncaDeveIncluirSenhaNoEvento()
    {
        UserCreatedEvent? publicado = null;

        _publisher.Setup(p => p.PublishAsync(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()))
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
