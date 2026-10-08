using FCG.Users.Domain.Entity;
using FCG.Users.Domain.Models.User;

namespace FCG.Users.Domain.Interfaces;
public interface IUserRepository
{
    Task<User?> GetByLoginAsync(string login);
    Task<User?> GetByEmailAsync(string email);
    Task<User> AddAsync(User user);
    Task<User?> GetByIdAsync(int id);
    Task<List<User>> GetAllAsync();
    Task<UserResponse> UpdateAsync(int id, UserUpdateRequest user);
    Task DeleteAsync(User user);
}