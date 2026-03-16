namespace UserService.Models;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FavoriteTeam { get; set; } = string.Empty;
}