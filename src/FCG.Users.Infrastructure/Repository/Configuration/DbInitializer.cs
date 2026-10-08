using Microsoft.EntityFrameworkCore;
using FCG.Users.Domain.Entity;

namespace FCG.Users.Infrastructure.Repository.Configuration;

public static class DbInitializer
{
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
        var users = new List<User>
        {
            new User(
                "Administrator",
                "admin@fiap.com",
                "admin",
                "Admin@123",
                PerfilEnum.Admin
            ),

            new User(
                "Barbara",
                "barbara@fiap.com",
                "Babi",
                "User@456",
                PerfilEnum.Client
            ),

            new User(
                "Micael",
                "micael2@fiap.com",
                "Micaelzin",
                "User@789",
                PerfilEnum.Client
            ),


            new User(
                "Priscila",
                "Priscila@fiap.com",
                "Pris",
                "User@012",
                PerfilEnum.Client
            ),

            new User(
                "Gustavo",
                "gustavo@fiap.com",
                "Gusta",
                "User@123",
                PerfilEnum.Client
            )
        };

        foreach (var user in users)
        {
            var userExists = await context.Users
                .AnyAsync(x => x.Login == user.Login);

            if (!userExists)
            {
                context.Users.Add(user);
            }
        }

        await context.SaveChangesAsync();
    }
}