using MassTransit;

namespace FCG_UsersDomain.Events;

/// <summary>
///     Evento disparado quando um usuário é criado. O NotificationAPI vai consumir.
/// </summary>
[EntityName("FCG_UserApi:user-created-event")]
public record UserCreatedEvent
{
    public int UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}