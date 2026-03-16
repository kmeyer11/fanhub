using Microsoft.AspNetCore.Mvc;
using UserService.Auth;
using UserService.Models;
using UserService.Repositories;

namespace UserService.Controllers;

[ApiController]
[Route("[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _repository;
    private readonly TokenService _tokenService;

    public UsersController(IUserRepository repository, TokenService tokenService)
    {
        _repository = repository;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var existing = await _repository.GetByEmailAsync(request.Email);
        if (existing is not null)
            return Conflict("Email already in use.");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FavoriteTeam = request.FavoriteTeam,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(user);
        return Ok("User registered successfully.");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _repository.GetByEmailAsync(request.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid email or password.");

        var token = _tokenService.GenerateToken(user);
        return Ok(new { token });
    }

    [HttpPut("{id}/team")]
    public async Task<IActionResult> UpdateFavoriteTeam(int id, [FromBody] string teamName)
    {
        await _repository.UpdateFavoriteTeamAsync(id, teamName);
        return NoContent();
    }
}