using FCG_UsersDomain.Entity;
using FCG_UsersDomain.Exceptions;
using FCG_UsersDomain.Models.User;

namespace FCG_UsersDomain.Interfaces;

public interface IUserService
{
    Task<UserResponse> CreateAsync(RegisterUserRequest request);

    /// <summary>Lança <see cref="UnauthorizedAccessException"/> quando o login ou a senha não conferem.</summary>
    Task<User> AuthenticateAsync(string login, string password);

    Task<IEnumerable<UserResponse>> GetAllAsync();

    /// <summary>Lança <see cref="NotFoundException"/> quando o usuário não existe.</summary>
    Task<UserResponse> GetByIdAsync(int id);

    /// <summary>Lança <see cref="NotFoundException"/> quando o usuário não existe.</summary>
    
    Task<UserResponse> UpdateAsync(int id, UserUpdateRequest request);
    Task DeleteAsync(int id);
}