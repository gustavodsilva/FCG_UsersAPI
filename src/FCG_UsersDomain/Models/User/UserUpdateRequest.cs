using FCG_UsersDomain.Entity;

namespace FCG_UsersDomain.Models.User;

public record UserUpdateRequest(string Name, string Email, string Login, PerfilEnum Perfil);
