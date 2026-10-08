using Moq;
using FCG.Users.Domain.Entity;
using FCG.Users.Domain.Exceptions;
using FCG.Users.Domain.Interfaces;
using FCG.Users.Domain.Models.User;
using FCG.Users.Domain.Services;

namespace FCG.Users.UnitTests;

public class UserServiceTests
{
    [Fact]
    public async Task ShouldThrowException_WhenLoginAlreadyExists()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>();

        var existingUser = new User(
            "Existing User",
            "existing@email.com",
            "gabriela",
            "Password@123",
            PerfilEnum.Client
        );

        repositoryMock
            .Setup(repository =>
                repository.GetByLoginAsync("gabriela"))
            .ReturnsAsync(existingUser);

        var service = new UserService(repositoryMock.Object, new Mock<IEventPublisher>().Object);

        var request = new RegisterUserRequest
        {
            Name = "Gabriela",
            Email = "new@email.com",
            Login = "gabriela",
            Password = "Password@123"
        };

        // Act + Assert
        await Assert.ThrowsAsync<ConflictException>(
            async () => await service.CreateAsync(request)
        );
    }

    [Fact]
    public async Task ShouldThrowException_WhenEmailAlreadyExists()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>();

        var existingUser = new User(
            "Existing User",
            "existing@email.com",
            "existinguser",
            "Password@123",
            PerfilEnum.Client
        );

        repositoryMock
            .Setup(repository =>
                repository.GetByLoginAsync("gabriela"))
            .ReturnsAsync((User?)null);

        repositoryMock
            .Setup(repository =>
                repository.GetByEmailAsync("existing@email.com"))
            .ReturnsAsync(existingUser);

        var service = new UserService(repositoryMock.Object, new Mock<IEventPublisher>().Object);

        var request = new RegisterUserRequest
        {
            Name = "Gabriela",
            Email = "existing@email.com",
            Login = "gabriela",
            Password = "Password@123"
        };

        // Act + Assert
        await Assert.ThrowsAsync<ConflictException>(
            async () => await service.CreateAsync(request)
        );
    }

    [Fact]
    public async Task ShouldCreateUser_WhenLoginAndEmailDoNotExist()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>();

        repositoryMock
            .Setup(repository =>
                repository.GetByLoginAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        repositoryMock
            .Setup(repository =>
                repository.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        repositoryMock
            .Setup(repository =>
                repository.AddAsync(It.IsAny<User>()))
            .ReturnsAsync((User user) =>
            {
                user.Id = 1;
                return user;
            });

        var service = new UserService(repositoryMock.Object, new Mock<IEventPublisher>().Object);

        var request = new RegisterUserRequest
        {
            Name = "Gabriela",
            Email = "gabriela@email.com",
            Login = "gabriela",
            Password = "Password@123"
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Gabriela", result.Name);
        Assert.Equal("gabriela@email.com", result.Email);
        Assert.Equal("gabriela", result.Login);

        repositoryMock.Verify(
            repository =>
                repository.AddAsync(It.IsAny<User>()),
            Times.Once
        );
    }

    // -----------------------------------------------------------------------
    // AuthenticateAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ShouldReturnUser_WhenLoginAndPasswordAreCorrect()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>();

        // O construtor de User já aplica o hash BCrypt na senha, então o serviço
        // recebe exatamente o formato que encontraria vindo do banco.
        var user = new User(
            "Gabriela",
            "gabriela@email.com",
            "gabriela",
            "Password@123",
            PerfilEnum.Client
        );

        repositoryMock
            .Setup(repository =>
                repository.GetByLoginAsync("gabriela"))
            .ReturnsAsync(user);

        var service = new UserService(repositoryMock.Object, new Mock<IEventPublisher>().Object);

        // Act
        var result = await service.AuthenticateAsync("gabriela", "Password@123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("gabriela", result.Login);
    }

    [Fact]
    public async Task ShouldThrowUnauthorized_WhenPasswordIsWrong()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>();

        var user = new User(
            "Gabriela",
            "gabriela@email.com",
            "gabriela",
            "Password@123",
            PerfilEnum.Client
        );

        repositoryMock
            .Setup(repository =>
                repository.GetByLoginAsync("gabriela"))
            .ReturnsAsync(user);

        var service = new UserService(repositoryMock.Object, new Mock<IEventPublisher>().Object);

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await service.AuthenticateAsync("gabriela", "SenhaErrada@1")
        );
    }

    [Fact]
    public async Task ShouldThrowUnauthorized_WhenLoginDoesNotExist()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>();

        repositoryMock
            .Setup(repository =>
                repository.GetByLoginAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        var service = new UserService(repositoryMock.Object, new Mock<IEventPublisher>().Object);

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await service.AuthenticateAsync("naoexiste", "Password@123")
        );
    }

    [Fact]
    public async Task ShouldUseTheSameMessage_ForWrongPasswordAndUnknownLogin()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>();

        var user = new User(
            "Gabriela",
            "gabriela@email.com",
            "gabriela",
            "Password@123",
            PerfilEnum.Client
        );

        repositoryMock
            .Setup(repository => repository.GetByLoginAsync("gabriela"))
            .ReturnsAsync(user);

        repositoryMock
            .Setup(repository => repository.GetByLoginAsync("naoexiste"))
            .ReturnsAsync((User?)null);

        var service = new UserService(repositoryMock.Object, new Mock<IEventPublisher>().Object);

        // Act
        var senhaErrada = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await service.AuthenticateAsync("gabriela", "SenhaErrada@1")
        );

        var loginInexistente = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await service.AuthenticateAsync("naoexiste", "Password@123")
        );

        // Assert
        // As mensagens não podem diferir: qualquer distinção confirmaria a um
        // atacante que o login existe.
        Assert.Equal(senhaErrada.Message, loginInexistente.Message);
    }

    // -----------------------------------------------------------------------
    // GetAllAsync / GetByIdAsync / DeleteAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ShouldReturnAllUsers()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>();

        var users = new List<User>
        {
            new("Gabriela", "gabriela@email.com", "gabriela", "Password@123", PerfilEnum.Client),
            new("Administrator", "admin@fiap.com", "admin", "Admin@123", PerfilEnum.Admin)
        };

        repositoryMock
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(users);

        var service = new UserService(repositoryMock.Object, new Mock<IEventPublisher>().Object);

        // Act
        var result = await service.GetAllAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, user => user.Login == "admin");
    }

    [Fact]
    public async Task ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>();

        var user = new User(
            "Gabriela",
            "gabriela@email.com",
            "gabriela",
            "Password@123",
            PerfilEnum.Client
        );

        user.Id = 1;

        repositoryMock
            .Setup(repository => repository.GetByIdAsync(1))
            .ReturnsAsync(user);

        var service = new UserService(repositoryMock.Object, new Mock<IEventPublisher>().Object);

        // Act
        var result = await service.GetByIdAsync(1);

        // Assert
        Assert.Equal(1, result.Id);
        Assert.Equal("gabriela@email.com", result.Email);
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenUserDoesNotExist()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>();

        repositoryMock
            .Setup(repository => repository.GetByIdAsync(999))
            .ReturnsAsync((User?)null);

        var service = new UserService(repositoryMock.Object, new Mock<IEventPublisher>().Object);

        // Act + Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await service.GetByIdAsync(999)
        );
    }

    [Fact]
    public async Task ShouldDeleteUser_WhenUserExists()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>();

        var user = new User(
            "Gabriela",
            "gabriela@email.com",
            "gabriela",
            "Password@123",
            PerfilEnum.Client
        );

        repositoryMock
            .Setup(repository => repository.GetByIdAsync(1))
            .ReturnsAsync(user);

        var service = new UserService(repositoryMock.Object, new Mock<IEventPublisher>().Object);

        // Act
        await service.DeleteAsync(1);

        // Assert
        repositoryMock.Verify(
            repository => repository.DeleteAsync(user),
            Times.Once
        );
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenDeletingNonExistingUser()
    {
        // Arrange
        var repositoryMock = new Mock<IUserRepository>();

        repositoryMock
            .Setup(repository => repository.GetByIdAsync(999))
            .ReturnsAsync((User?)null);

        var service = new UserService(repositoryMock.Object, new Mock<IEventPublisher>().Object);

        // Act + Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await service.DeleteAsync(999)
        );

        repositoryMock.Verify(
            repository => repository.DeleteAsync(It.IsAny<User>()),
            Times.Never
        );
    }
}
