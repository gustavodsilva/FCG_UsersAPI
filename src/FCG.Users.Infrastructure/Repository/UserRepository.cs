using Microsoft.EntityFrameworkCore;
using FCG.Users.Domain.Entity;
using FCG.Users.Domain.Interfaces;
using FCG.Users.Domain.Models.User;

namespace FCG.Users.Infrastructure.Repository;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByLoginAsync(string login)
    {
        return await _context.Users.FirstOrDefaultAsync(x => x.Login == login);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(x => x.Email == email);
    }

    public async Task<User> AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        return user;
    }
    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _context.Users.AsNoTracking().ToListAsync();
    }

    public async Task<UserResponse> UpdateAsync(int id, UserUpdateRequest user)
    {
        var userUpdate = _context.Users.FirstOrDefault(u => u.Id == id);

        if (userUpdate != null)
        {
            userUpdate.Name = user.Name;
            userUpdate.Email = user.Email;
            userUpdate.Login = user.Login;
            userUpdate.Perfil = user.Perfil;

            _context.Users.Update(userUpdate);
            await _context.SaveChangesAsync();
        }

        return new UserResponse
        {
            Id = userUpdate.Id,
            Name = userUpdate.Name,
            Email = userUpdate.Email,
            Login = userUpdate.Login,
            Perfil = userUpdate.Perfil,
            CreatedAt = userUpdate.CreatedAt
        };
    }

    public async Task DeleteAsync(User user)
    {
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }
}