using FCG_UsersDomain.Events;
using FCG_UsersDomain.Entity;
using FCG_UsersDomain.Exceptions;
using FCG_UsersDomain.Interfaces;
using FCG_UsersDomain.Models.User;

namespace FCG_UsersDomain.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IEventPublisher _eventPublisher;

    public UserService(IUserRepository userRepository, IEventPublisher eventPublisher)
    {
        _userRepository = userRepository;
        _eventPublisher = eventPublisher;
    }

    public async Task<UserResponse> CreateAsync(RegisterUserRequest request)
    {
        var existingLogin = await _userRepository.GetByLoginAsync(request.Login);

        if (existingLogin != null)
            throw new ConflictException("Login já cadastrado.");

        var existingEmail = await _userRepository.GetByEmailAsync(request.Email);

        if (existingEmail != null)
            throw new ConflictException("E-mail já cadastrado.");

        var user = new User(
            request.Name,
            request.Email,
            request.Login,
            request.Password,
            PerfilEnum.Client
        );

        var createdUser = await _userRepository.AddAsync(user);

        // Fluxo de cadastro: o NotificationsAPI consome este evento e envia o
        // e-mail de boas-vindas. Só publicamos depois que o usuário foi gravado.
        await _eventPublisher.Publish(new UserCreatedEvent
        {
            UserId = createdUser.Id,
            Name = createdUser.Name,
            Email = createdUser.Email
        });

        return new UserResponse
        {
            Id = createdUser.Id,
            Name = createdUser.Name,
            Email = createdUser.Email,
            Login = createdUser.Login,
            Perfil = createdUser.Perfil
        };
    }

    public async Task<User> AuthenticateAsync(string login, string password)
    {
        var user = await _userRepository.GetByLoginAsync(login);

        // A mesma mensagem para login inexistente e senha errada: dizer qual dos dois
        // falhou entregaria a um atacante a confirmação de que o login existe.
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            throw new UnauthorizedAccessException("Usuário ou senha inválidos.");

        return user;
    }

    public async Task<IEnumerable<UserResponse>> GetAllAsync()
    {
        var users = await _userRepository.GetAllAsync();

        return users.Select(user => new UserResponse
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Login = user.Login,
            Perfil = user.Perfil,
            CreatedAt = user.CreatedAt
        });
    }

    public async Task<UserResponse> GetByIdAsync(int id)
    {
        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Usuário {id} não encontrado.");

        return new UserResponse
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Login = user.Login,
            Perfil = user.Perfil,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task DeleteAsync(int id)
    {
        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Usuário {id} não encontrado.");

        await _userRepository.DeleteAsync(user);
    }

    public async Task<UserResponse> UpdateAsync(int id, UserUpdateRequest request)
    {
        var userUpdated = await _userRepository.UpdateAsync(id, request);
        return userUpdated;
    }
}