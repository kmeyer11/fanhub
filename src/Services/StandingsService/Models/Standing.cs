namespace StandingsService.Models;

public class Standing
{
    public int Id { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string League { get; set; } = string.Empty;
    public int Season { get; set; }
    public int Rank { get; set; }
    public int Points { get; set; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
}