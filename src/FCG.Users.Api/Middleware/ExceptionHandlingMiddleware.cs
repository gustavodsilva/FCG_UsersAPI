using Microsoft.AspNetCore.Mvc;
using FCG.Users.Domain.Exceptions;

namespace FCG.Users.Api.Middleware;

/// <summary>
/// Captura toda exceção não tratada da aplicação, registra o log estruturado
/// e devolve uma resposta padronizada no formato ProblemDetails (RFC 7807).
///
/// O cliente sempre recebe o mesmo TraceId que aparece no log, o que permite
/// localizar no log exatamente o erro que ele viu.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>
    /// O ASP.NET Core injeta o próximo middleware do pipeline e o logger automaticamente.
    /// </summary>
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Executa o restante do pipeline e trata qualquer exceção que suba até aqui.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = MapException(exception);
        var isUnexpectedError = statusCode == StatusCodes.Status500InternalServerError;

        WriteLog(context, exception, statusCode, isUnexpectedError);

        // Se a resposta já começou a ser enviada, não é mais possível alterá-la.
        // O log acima já foi gravado, então apenas encerramos aqui.
        if (context.Response.HasStarted)
        {
            return;
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Instance = context.Request.Path,

            Detail = isUnexpectedError ? "Ocorreu um erro inesperado. Informe o traceId ao suporte." : exception.Message
        };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json");
    }

    /// <summary>
    /// Grava o log estruturado do erro.
    ///
    /// Os valores entre chaves NÃO são interpolação de string: cada um vira uma
    /// propriedade nomeada e pesquisável no log (ex.: filtrar por StatusCode = 500).
    /// Por isso nunca use $"..." aqui — isso transformaria tudo em texto puro.
    /// </summary>
    private void WriteLog(HttpContext context, Exception exception, int statusCode, bool isUnexpectedError)
    {
        if (isUnexpectedError)
        {
            _logger.LogError(
                exception,
                "Erro inesperado em {RequestMethod} {RequestPath}. StatusCode: {StatusCode}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path.Value,
                statusCode,
                context.TraceIdentifier);

            return;
        }

        // Erro de negócio é esperado: nível Warning e sem stack trace, que não agregaria nada.
        _logger.LogWarning(
            "Requisição rejeitada em {RequestMethod} {RequestPath}. StatusCode: {StatusCode}. TraceId: {TraceId}. Motivo: {Reason}",
            context.Request.Method,
            context.Request.Path.Value,
            statusCode,
            context.TraceIdentifier,
            exception.Message);
    }

    /// <summary>
    /// Traduz o tipo da exceção no status HTTP correspondente.
    ///
    /// A ordem importa: NotFoundException e ConflictException herdam de DomainException
    /// e precisam vir antes dela, senão nunca seriam alcançadas.
    ///
    /// Só exceções do nosso domínio são traduzidas em 4xx. Exceções do .NET
    /// (ArgumentException, InvalidOperationException e afins) caem no 500 de propósito:
    /// quando aparecem, são bug nosso, não erro de quem chamou. Mapeá-las para 400
    /// esconderia a falha do monitoramento e ainda exporia a mensagem interna —
    /// o Detail só é devolvido ao cliente quando o status não é 500.
    /// </summary>
    private static (int StatusCode, string Title) MapException(Exception exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
        ConflictException => (StatusCodes.Status409Conflict, "Conflito com o estado atual do recurso"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Acesso restrito"),
        DomainException => (StatusCodes.Status400BadRequest, "Requisição inválida"),
        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Não autorizado"),
        _ => (StatusCodes.Status500InternalServerError, "Erro interno do servidor")
    };
}