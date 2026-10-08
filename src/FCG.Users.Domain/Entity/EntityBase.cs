namespace FCG.Users.Domain.Entity;

/// <summary>
/// O que toda entidade do domínio tem em comum: identidade e datas de auditoria.
///
/// UpdatedAt é anulável porque um registro recém-criado ainda não foi atualizado —
/// nulo aqui significa "nunca sofreu alteração", que é diferente de repetir a data
/// de criação.
/// </summary>
public class EntityBase
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
}
