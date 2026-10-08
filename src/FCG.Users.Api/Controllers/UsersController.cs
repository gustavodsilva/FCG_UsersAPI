using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FCG.Users.Domain.Interfaces;
using FCG.Users.Domain.Models.User;

namespace FCG.Users.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Consulta todos os usuários cadastrados (apenas para administradores)
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _userService.GetAllAsync());
    }

    /// <summary>
    /// Consulta um usuário específico pelo ID (apenas para administradores)
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        return Ok(await _userService.GetByIdAsync(id));
    }

    ///<summary>
    ///Atualiza um usuário específico pelo ID (apenas para administradores)
    ///</summary>
    ///<param name="id"></param>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UserUpdateRequest request)
    {
        return Ok(await _userService.UpdateAsync(id, request));
    }

    /// <summary>
    /// Deleta um usuário específico pelo ID (apenas para administradores)
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _userService.DeleteAsync(id);

        return NoContent();
    }
}