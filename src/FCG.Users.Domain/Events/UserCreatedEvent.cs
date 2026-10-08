namespace FCG.Contracts.Events;

/// <summary>
///     Evento disparado quando um usuário é criado. O NotificationAPI vai consumir.
/// </summary>
public record UserCreatedEvent
{
    public int UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
