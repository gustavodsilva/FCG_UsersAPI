namespace FCG.Users.Domain.Exceptions;

/// <summary>
/// Regra de negócio violada por causa do estado atual do recurso, e não por erro
/// no que o cliente enviou — por exemplo, cadastrar um login que já existe.
///
/// A requisição em si é válida: repeti-la sem mudar nada continuaria falhando até
/// que o estado do servidor mude. Por isso o middleware devolve 409 Conflict, e não
/// o 400 usado para dados inválidos.
/// </summary>
public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}
