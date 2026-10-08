using Moq;
using Reqnroll;
using FCG.Users.Domain.Entity;
using FCG.Users.Domain.Interfaces;
using FCG.Users.Domain.Models.User;
using FCG.Users.Domain.Services;

namespace FCG.Users.UnitTests.Features.Steps;

[Binding]
public class UserRegistrationSteps
{
    private readonly Mock<IUserRepository> _userRepositoryMock;

    private readonly UserService _userService;

    private RegisterUserRequest _request = null!;

    private UserResponse? _response;

    private Exception? _exception;

    private User? _createdUser;

    public UserRegistrationSteps()
    {
        _userRepositoryMock =
            new Mock<IUserRepository>();

        _userService =
            new UserService(_userRepositoryMock.Object, new Mock<IEventPublisher>().Object);

        ConfigureDefaultRepositoryBehavior();
    }

    private void ConfigureDefaultRepositoryBehavior()
    {
        _userRepositoryMock.Setup(repository => repository.GetByLoginAsync( It.IsAny<string>())).ReturnsAsync((User?)null);

        _userRepositoryMock.Setup(repository => repository.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        _userRepositoryMock.Setup(repository => repository.AddAsync(It.IsAny<User>())).ReturnsAsync((User user) => {user.Id = 1; _createdUser = user; return user;});
    }

    [Given("I provide valid user registration data")]
    public void GivenIProvideValidUserRegistrationData()
    {
        _request = new RegisterUserRequest
        {
            Name = "John Doe",
            Email = "john.doe@email.com",
            Login = "johndoe",
            Password = "Password@123"
        };
    }

    [Given("I provide user registration data with invalid email")]
    public void GivenIProvideUserRegistrationDataWithInvalidEmail()
    {
        _request = new RegisterUserRequest
        {
            Name = "John Doe",
            Email = "invalid-email",
            Login = "johndoe",
            Password = "Password@123"
        };
    }

    [Given("I provide user registration data with a short password")]
    public void GivenIProvideUserRegistrationDataWithShortPassword()
    {
        _request = new RegisterUserRequest
        {
            Name = "John Doe",
            Email = "john.doe@email.com",
            Login = "johndoe",

            // 7 characters
            Password = "Ab@1234"
        };
    }

    [Given("I provide user registration data with a password without letters")]
    public void GivenIProvideUserRegistrationDataWithPasswordWithoutLetters()
    {
        _request = new RegisterUserRequest
        {
            Name = "John Doe",
            Email = "john.doe@email.com",
            Login = "johndoe",
            Password = "12345678@"
        };
    }

    [Given("I provide user registration data with a password without numbers")]
    public void GivenIProvideUserRegistrationDataWithPasswordWithoutNumbers()
    {
        _request = new RegisterUserRequest
        {
            Name = "John Doe",
            Email = "john.doe@email.com",
            Login = "johndoe",
            Password = "Password@"
        };
    }

    [Given("I provide user registration data with a password without special characters")]
    public void GivenIProvideUserRegistrationDataWithPasswordWithoutSpecialCharacters()
    {
        _request = new RegisterUserRequest
        {
            Name = "John Doe",
            Email = "john.doe@email.com",
            Login = "johndoe",
            Password = "Password123"
        };
    }

    [Given("I provide user registration data with an already registered login")]
    public void GivenIProvideUserRegistrationDataWithAlreadyRegisteredLogin()
    {
        _request = new RegisterUserRequest
        {
            Name = "John Doe",
            Email = "john.doe@email.com",
            Login = "johndoe",
            Password = "Password@123"
        };

        var existingUser = new User(
            "Existing User",
            "existing@email.com",
            "johndoe",
            "Password@123",
            PerfilEnum.Client
        );

        _userRepositoryMock.Setup(repository => repository.GetByLoginAsync("johndoe")).ReturnsAsync(existingUser);
    }

    [Given("I provide user registration data with an already registered email")]
    public void GivenIProvideUserRegistrationDataWithAlreadyRegisteredEmail()
    {
        _request = new RegisterUserRequest
        {
            Name = "John Doe",
            Email = "john.doe@email.com",
            Login = "johndoe",
            Password = "Password@123"
        };

        var existingUser = new User(
            "Existing User",
            "john.doe@email.com",
            "existinglogin",
            "Password@123",
            PerfilEnum.Client
        );

        _userRepositoryMock.Setup(repository => repository.GetByLoginAsync("johndoe")).ReturnsAsync((User?)null);

        _userRepositoryMock.Setup(repository => repository.GetByEmailAsync("john.doe@email.com")).ReturnsAsync(existingUser);
    }

    [When("I request the user registration")]
    public async Task WhenIRequestTheUserRegistration()
    {
        try
        {
            _response = await _userService.CreateAsync(_request);
        }
        catch (Exception exception)
        {
            _exception = exception;
        }
    }

    [Then("the user should be registered successfully")]
    public void ThenTheUserShouldBeRegisteredSuccessfully()
    {
        Assert.Null(_exception);

        Assert.NotNull(_response);

        Assert.Equal("John Doe",_response.Name);

        Assert.Equal("john.doe@email.com", _response.Email);

        Assert.Equal("johndoe", _response.Login);

        Assert.Equal(1, _response.Id);

        _userRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Once);
    }

    [Then("the registered user should have the Client profile")]
    public void ThenTheRegisteredUserShouldHaveTheClientProfile()
    {
        Assert.NotNull(_response);

        Assert.Equal(PerfilEnum.Client,_response.Perfil);
    }

    [Then(
        "the registration should fail with the message {string}"
    )]
    public void ThenTheRegistrationShouldFailWithTheMessage(
        string expectedMessage)
    {
        Assert.NotNull(_exception);

        Assert.Equal(expectedMessage,_exception.Message);
    }

    [Then("the password should not be stored as plain text")]
    public void ThenThePasswordShouldNotBeStoredAsPlainText()
    {
        Assert.NotNull(_createdUser);

        Assert.NotEqual("Password@123", _createdUser.Password);

        Assert.True(BCrypt.Net.BCrypt.Verify("Password@123", _createdUser.Password));
    }
}