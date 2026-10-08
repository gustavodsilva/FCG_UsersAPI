using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using FCG.Users.Domain.Interfaces;
using FCG.Users.Domain.Services;
using FCG.Users.Infrastructure.Authentication;
using FCG.Users.Infrastructure.Repository;

namespace FCG.Users.Api.Configuration;

/// <summary>
/// Registra os serviços da camada de API (controllers, autenticação e Swagger).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra serviços de infraestrutura (DbContext, repositories, etc.).
    /// Mantém Program.cs enxuto delegando a configuração aqui.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Tenta obter connection string nomeada "SqlConnection" ou fallback para "DefaultConnection"
        var connectionString = configuration.GetConnectionString("SqlConnection")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(
                connectionString,
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure();
                }
            );
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }

    /// <summary>
    /// Escreve a resposta de erro no mesmo formato ProblemDetails (RFC 7807) usado
    /// pelo ExceptionHandlingMiddleware, para que quem consome a API trate todos os
    /// erros da mesma maneira — inclusive os gerados pelo pipeline de autorização.
    /// </summary>
    private static async Task WriteProblemDetails(
        HttpContext context,
        int statusCode,
        string title,
        string detail)
    {
        // Se a resposta já começou a ser enviada, não é mais possível alterá-la.
        if (context.Response.HasStarted)
        {
            return;
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json");
    }

    /// <summary>
    /// Autenticação por token JWT, conforme exigido pelo desafio.
    /// As configurações vêm da seção "Jwt" do appsettings — as mesmas usadas pelo
    /// TokenService para assinar, de modo que assinatura e validação sempre batem.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtSettings.SecaoConfiguracao).Get<JwtSettings>() 
            ?? 
            throw new InvalidOperationException("A seção 'Jwt' não foi encontrada na configuração.");

        if (jwt.SecretKey.Length < 32)
        {
            // O HMAC-SHA256 exige uma chave de 256 bits. Com menos que isso a
            // aplicação só falharia ao validar o primeiro token, em runtime.
            throw new InvalidOperationException("A chave 'Jwt:SecretKey' precisa ter no mínimo 32 caracteres.");
        }

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwt.SecretKey)),

                // Sem isso o .NET aceita um token até 5 minutos após expirar.
                ClockSkew = TimeSpan.Zero
            };


            // 401 e 403 não passam pelo ExceptionHandlingMiddleware: o pipeline de
            // autenticação/autorização encerra a requisição antes dos controllers,
            // sem lançar exceção. Estes eventos são o lugar de padronizar as duas
            // respostas no mesmo formato ProblemDetails do resto da API.
            options.Events = new JwtBearerEvents
            {
                // Token ausente, inválido ou expirado.
                OnChallenge = context =>
                {
                    // Sem isto o ASP.NET escreveria a resposta vazia padrão por cima
                    // da nossa.
                    context.HandleResponse();

                    // HandleResponse também cancela a escrita automática do
                    // WWW-Authenticate — e a RFC 9110 exige esse header no 401.
                    // Reproduzimos aqui os mesmos dois valores que o framework
                    // produzia: "Bearer" quando não veio token, e
                    // Bearer error="invalid_token" quando o token foi recusado.
                    context.Response.Headers.WWWAuthenticate = string.IsNullOrEmpty(context.Error)
                        ? "Bearer"
                        : $"Bearer error=\"{context.Error}\"";

                    var tokenExpirado = context.AuthenticateFailure is SecurityTokenExpiredException;

                    return WriteProblemDetails(
                        context.HttpContext,
                        StatusCodes.Status401Unauthorized,
                        "Não autenticado",
                        tokenExpirado
                            ? "Seu token expirou. Autentique-se novamente em /api/auth/login."
                            : "Informe um token válido no cabeçalho Authorization, no formato: Bearer {token}.");
                },

                // Autenticado, mas o perfil não tem permissão para o endpoint.
                OnForbidden = context => WriteProblemDetails(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "Acesso negado",
                    "Seu perfil não tem permissão para acessar este recurso.")
            };
        });

        return services;
    }

    /// <summary>
    /// Documentação da API no Swagger, com suporte a autenticação por token.
    /// </summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(swagger =>
        {
            swagger.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "FCG - Users API",
                Version = "v1",
                Description = "Microsserviço de usuários da FIAP Cloud Games: cadastro, autenticação (JWT) e autorização.",
                Contact = new OpenApiContact
                {
                    Name = "Equipe Desafio",
                    Email = "contato@desafio.com"
                }
            });

            // Campo para informar o token nas requisições feitas pelo Swagger.
            swagger.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description =
                    "Informe o token no formato: Bearer {seu token}\n\n" +
                    "Atenção: chamar /auth/login NÃO troca o token daqui. " +
                    "Ao trocar de usuário, volte neste botão e cole o token novo — " +
                    "caso contrário as requisições continuarão sendo feitas como o usuário anterior.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            swagger.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Traz para o Swagger os comentários <summary> dos controllers.
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                swagger.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}