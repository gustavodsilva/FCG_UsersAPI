using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using FCG.Users.Domain.Exceptions;
using FCG.Users.Api.Middleware;

namespace FCG.Users.UnitTests.Middleware;

/// <summary>
/// Testa o middleware isoladamente, sem subir a aplicação: monta um HttpContext
/// falso, faz o "próximo middleware" lançar a exceção desejada e verifica o que
/// sai na resposta e no log.
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    // -----------------------------------------------------------------------
    // Tradução de exceção em status HTTP
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ShouldReturn404_WhenNotFoundExceptionIsThrown()
    {
        // Arrange + Act
        var (context, problem) = await ExecutarComExcecao(
            new NotFoundException("Jogo 999 não encontrado."));

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal("Recurso não encontrado", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ShouldReturn409_WhenConflictExceptionIsThrown()
    {
        // Arrange + Act
        var (context, problem) = await ExecutarComExcecao(
            new ConflictException("Login já cadastrado."));

        // Assert
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal(
            "Conflito com o estado atual do recurso",
            problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ShouldReturn400_WhenDomainExceptionIsThrown()
    {
        // Arrange + Act
        var (context, problem) = await ExecutarComExcecao(
            new DomainException("O preço do jogo não pode ser negativo."));

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("Requisição inválida", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ShouldReturn401_WhenUnauthorizedAccessExceptionIsThrown()
    {
        // Arrange + Act
        var (context, problem) = await ExecutarComExcecao(
            new UnauthorizedAccessException("Usuário ou senha inválidos."));

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("Não autorizado", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ShouldReturn500_WhenUnexpectedExceptionIsThrown()
    {
        // Arrange + Act
        var (context, problem) = await ExecutarComExcecao(
            new Exception("Falha qualquer."));

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("Erro interno do servidor", problem.GetProperty("title").GetString());
    }

    /// <summary>
    /// NotFoundException e ConflictException herdam de DomainException. Se a ordem
    /// do switch for trocada, elas passam a cair no 400 — este teste trava isso.
    /// </summary>
    [Fact]
    public async Task ShouldPreferTheMostSpecificException_WhenItInheritsFromDomainException()
    {
        // Arrange + Act
        var (naoEncontrado, _) = await ExecutarComExcecao(new NotFoundException("x"));
        var (conflito, _) = await ExecutarComExcecao(new ConflictException("x"));
        var (dominio, _) = await ExecutarComExcecao(new DomainException("x"));

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, naoEncontrado.Response.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, conflito.Response.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, dominio.Response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Exceções do .NET não podem virar 4xx: quando aparecem, são bug nosso
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(ArgumentNullException))]
    [InlineData(typeof(ArgumentOutOfRangeException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(NullReferenceException))]
    public async Task ShouldReturn500_WhenExceptionComesFromTheFrameworkAndNotFromTheDomain(
        Type tipoDaExcecao)
    {
        // Arrange
        var excecao = (Exception)Activator.CreateInstance(tipoDaExcecao)!;

        // Act
        var (context, _) = await ExecutarComExcecao(excecao);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task ShouldNotLeakTheOriginalMessage_WhenErrorIsUnexpected()
    {
        // Arrange
        const string mensagemInterna =
            "The instance of entity type 'UserGame' cannot be tracked because another instance " +
            "with the key value '{Id: 1}' is already being tracked.";

        // Act
        var (_, problem) = await ExecutarComExcecao(new InvalidOperationException(mensagemInterna));

        // Assert
        var detail = problem.GetProperty("detail").GetString();

        Assert.DoesNotContain("entity type", detail);
        Assert.Equal("Ocorreu um erro inesperado. Informe o traceId ao suporte.", detail);
    }

    [Fact]
    public async Task ShouldExposeTheMessage_WhenErrorIsFromTheDomain()
    {
        // Arrange + Act
        var (_, problem) = await ExecutarComExcecao(
            new NotFoundException("Jogo 999 não encontrado."));

        // Assert
        // Mensagem de negócio é escrita para o usuário final, então pode ser exposta.
        Assert.Equal("Jogo 999 não encontrado.", problem.GetProperty("detail").GetString());
    }

    // -----------------------------------------------------------------------
    // Formato da resposta (RFC 7807)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ShouldRespondAsProblemJson_WithTraceIdAndInstance()
    {
        // Arrange + Act
        var (context, problem) = await ExecutarComExcecao(
            new NotFoundException("Jogo 999 não encontrado."),
            path: "/Game/999");

        // Assert
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal("/Game/999", problem.GetProperty("instance").GetString());
        Assert.Equal(404, problem.GetProperty("status").GetInt32());

        // O traceId devolvido precisa ser o mesmo que vai para o log — é ele que
        // permite achar no log exatamente o erro que o usuário viu.
        Assert.Equal(
            context.TraceIdentifier,
            problem.GetProperty("traceId").GetString());
    }

    // -----------------------------------------------------------------------
    // Comportamento do pipeline
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ShouldNotTouchTheResponse_WhenThereIsNoException()
    {
        // Arrange
        var context = CriarContexto();
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();

        var middleware = new ExceptionHandlingMiddleware(
            _ =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task ShouldKeepTheStatusCode_WhenResponseHasAlreadyStarted()
    {
        // Arrange
        var context = CriarContexto();

        // Simula uma resposta que já começou a ser enviada ao cliente: nesse ponto
        // os headers já foram para a rede e o status não pode mais ser alterado.
        context.Features.Set<IHttpResponseFeature>(new RespostaJaIniciada
        {
            StatusCode = StatusCodes.Status200OK
        });

        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new NotFoundException("Jogo 999 não encontrado."),
            logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Log
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ShouldLogAsError_WhenErrorIsUnexpected()
    {
        // Arrange
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();

        // Act
        await ExecutarComExcecao(new Exception("Falha qualquer."), logger: logger);

        // Assert
        VerificarLog(logger, LogLevel.Error, Times.Once());
        VerificarLog(logger, LogLevel.Warning, Times.Never());
    }

    [Fact]
    public async Task ShouldLogAsWarning_WhenErrorIsFromTheDomain()
    {
        // Arrange
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();

        // Act
        await ExecutarComExcecao(new NotFoundException("Jogo 999 não encontrado."), logger: logger);

        // Assert
        // Erro de negócio é esperado: polui o log se subir para Error.
        VerificarLog(logger, LogLevel.Warning, Times.Once());
        VerificarLog(logger, LogLevel.Error, Times.Never());
    }

    [Fact]
    public async Task ShouldLogTheException_WhenErrorIsUnexpected()
    {
        // Arrange
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var excecao = new Exception("Falha qualquer.");

        // Act
        await ExecutarComExcecao(excecao, logger: logger);

        // Assert
        // O stack trace precisa chegar ao log: é a única cópia dele, já que a
        // resposta devolve apenas a mensagem genérica.
        logger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                excecao,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // Apoio
    // -----------------------------------------------------------------------

    private static DefaultHttpContext CriarContexto(string path = "/api/teste")
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "TRACE-DE-TESTE"
        };

        context.Request.Method = "GET";
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        return context;
    }

    /// <summary>
    /// Roda o middleware com um "próximo" que sempre lança a exceção informada e
    /// devolve o contexto e o corpo da resposta já desserializado.
    /// </summary>
    private static async Task<(DefaultHttpContext Context, JsonElement Problem)> ExecutarComExcecao(
        Exception excecao,
        string path = "/api/teste",
        Mock<ILogger<ExceptionHandlingMiddleware>>? logger = null)
    {
        var context = CriarContexto(path);

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw excecao,
            (logger ?? new Mock<ILogger<ExceptionHandlingMiddleware>>()).Object);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);

        var corpo = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return (context, JsonSerializer.Deserialize<JsonElement>(corpo));
    }

    private static void VerificarLog(
        Mock<ILogger<ExceptionHandlingMiddleware>> logger,
        LogLevel nivel,
        Times vezes) =>
        logger.Verify(
            log => log.Log(
                nivel,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            vezes);

    /// <summary>
    /// Feature de resposta que se declara já iniciada, para exercitar a guarda de
    /// Response.HasStarted do middleware.
    /// </summary>
    private sealed class RespostaJaIniciada : IHttpResponseFeature
    {
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted => true;
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public string? ReasonPhrase { get; set; }
        public int StatusCode { get; set; }

        public void OnCompleted(Func<object, Task> callback, object state) { }
        public void OnStarting(Func<object, Task> callback, object state) { }
    }
}
