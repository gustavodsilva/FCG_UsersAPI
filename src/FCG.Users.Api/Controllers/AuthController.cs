using Microsoft.AspNetCore.Mvc;
using FCG.Users.Domain.Interfaces;
using FCG.Users.Domain.Models.User;

namespace FCG.Users.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly IUserService _userService;

    public AuthController(ITokenService tokenService, IUserService userService)
    {
        _tokenService = tokenService;
        _userService = userService;
    }

    /// <summary>
    /// Registra usuário
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        var createdUser =  await _userService.CreateAsync(request);

        return Created($"/api/users/{createdUser.Id}", createdUser);
    }

    /// <summary>
    /// Login
    /// </summary>
    /// <param name="login"></param>
    /// <returns></returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(GeneratedToken), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] UserLogin login)
    {
        var user = await _userService.AuthenticateAsync(login.Login, login.Password);

        var token = _tokenService.GenerateToken(user);

        return Ok(token);
    }
}