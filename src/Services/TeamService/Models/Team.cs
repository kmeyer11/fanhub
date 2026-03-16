namespace TeamService.Models;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string League { get; set; } = string.Empty;
    public string Stadium { get; set; } = string.Empty;
    public int Founded { get; set; }
    public string BadgeUrl { get; set; } = string.Empty;
}