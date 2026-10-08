using FCG.Users.Domain.Entity;

namespace FCG.Users.Domain.Models.User;

public record UserUpdateRequest(string Name, string Email, string Login, PerfilEnum Perfil);
