using FCG.Users.Domain.Entity;
using FCG.Users.Domain.Exceptions;

namespace FCG.Users.UnitTests;

public class UserTests
{
    [Fact]
    public void ShouldCreateUser_WhenDataIsValid()
    {
        // Arrange + Act
        var user = new User(
            "Gabriela",
            "gabriela@email.com",
            "gabriela",
            "Password@123",
            PerfilEnum.Client           
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal("Gabriela", user.Name);
        Assert.Equal("gabriela@email.com", user.Email);
        Assert.Equal("gabriela", user.Login);
        Assert.Equal(PerfilEnum.Client, user.Perfil);
    }

    [Fact]
    public void ShouldThrowException_WhenPasswordHasLessThanEightCharacters()
    {
        Assert.Throws<DomainException>(() =>
        {
            new User(
                "Gabriela",
                "gabriela@email.com",
                "gabriela",
                "Ab@123",
                PerfilEnum.Client
            );
        });
    }

    [Fact]
    public void ShouldThrowException_WhenPasswordDoesNotContainNumber()
    {
        Assert.Throws<DomainException>(() =>
        {
            new User(
                "Gabriela",
                "gabriela@email.com",
                "gabriela",
                "Password@",
                PerfilEnum.Client
            );
        });
    }

    [Fact]
    public void ShouldThrowException_WhenPasswordDoesNotContainLetter()
    {
        Assert.Throws<DomainException>(() =>
        {
            new User(
                "Gabriela",
                "gabriela@email.com",
                "gabriela",
                "12345678@",
                PerfilEnum.Client
            );
        });
    }

    [Fact]
    public void ShouldThrowException_WhenPasswordDoesNotContainSpecialCharacter()
    {
        Assert.Throws<DomainException>(() =>
        {
            new User(
                "Gabriela",
                "gabriela@email.com",
                "gabriela",
                "Password123",
                PerfilEnum.Client
            );
        });
    }

    [Fact]
    public void ShouldThrowException_WhenEmailIsInvalid()
    {
        Assert.Throws<DomainException>(() =>
        {
            new User(
                "Gabriela",
                "invalid-email",
                "gabriela",
                "Password@123",
                PerfilEnum.Client
            );
        });
    }

    [Fact]
    public void ShouldSetCreatedAt_WhenUserIsCreated()
    {
        // Arrange
        var beforeCreation = DateTime.Now;

        // Act
        var user = new User(
            "Gabriela",
            "gabriela@email.com",
            "gabriela",
            "Password@123",
            PerfilEnum.Client
        );

        var afterCreation = DateTime.Now;

        // Assert
        Assert.InRange(
            user.CreatedAt,
            beforeCreation,
            afterCreation
        );
    }

    [Fact]
    public void ShouldThrowException_WhenUpdatingWithWeakPassword()
    {
        // Arrange
        var user = new User(
            "Gabriela",
            "gabriela@email.com",
            "gabriela",
            "Password@123",
            PerfilEnum.Client
        );

        // Act + Assert
        Assert.Throws<DomainException>(() =>
        {
            user.Update(
                "Gabriela",
                "gabriela@email.com",
                "123",
                PerfilEnum.Client
            );
        });
    }

    [Fact]
    public void ShouldThrowException_WhenUpdatingWithInvalidEmail()
    {
        // Arrange
        var user = new User(
            "Gabriela",
            "gabriela@email.com",
            "gabriela",
            "Password@123",
            PerfilEnum.Client
        );

        // Act + Assert
        Assert.Throws<DomainException>(() =>
        {
            user.Update(
                "Gabriela",
                 "invalid-email",
                "Password@123",
                PerfilEnum.Client
            );
        });
    }
}