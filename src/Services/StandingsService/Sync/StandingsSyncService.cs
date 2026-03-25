using System.Data;
using System.Text.Json;
using Dapper;

namespace StandingsService.Sync;

public class StandingsSyncService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StandingsSyncService> _logger;
    private readonly HttpClient _httpClient;

    private static readonly Dictionary<int, string> LeagueNames = new()
    {
        { 39,  "Premier League" },
        { 119, "Superliga"      },
        { 140, "La Liga"        },
    };

    public StandingsSyncService(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<StandingsSyncService> logger,
        HttpClient httpClient)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClient = httpClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting standings sync...");
                await SyncAllLeaguesAsync();
                _logger.LogInformation("Standings sync complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during standings sync.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task SyncAllLeaguesAsync()
    {
        var apiKey = _config["ApiFootball:ApiKey"];
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-apisports-key", apiKey);

        var season = await ResolveLatestSupportedSeasonAsync();
        _logger.LogInformation("Using season {Season} for standings sync.", season);

        foreach (var (leagueId, leagueName) in LeagueNames)
        {
            try
            {
                _logger.LogInformation("Syncing standings for league {LeagueId}...", leagueId);
                await SyncLeagueStandingsAsync(leagueId, leagueName, season);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing standings for league {LeagueId}.", leagueId);
            }
        }
    }

    private async Task<int> ResolveLatestSupportedSeasonAsync()
    {
        var candidate = DateTime.UtcNow.Month >= 7 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;
        for (var season = candidate; season >= candidate - 3; season--)
        {
            var probe = await _httpClient.GetAsync(
                $"https://v3.football.api-sports.io/standings?league=39&season={season}");
            var doc = JsonDocument.Parse(await probe.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("results", out var r) && r.GetInt32() > 0)
                return season;
        }
        return candidate - 1;
    }

    private async Task SyncLeagueStandingsAsync(int leagueId, string leagueName, int season)
    {
        var response = await _httpClient.GetAsync(
            $"https://v3.football.api-sports.io/standings?league={leagueId}&season={season}");
        response.EnsureSuccessStatusCode();

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var leagueEl = doc.RootElement.GetProperty("response")[0].GetProperty("league");
        var standingsArray = leagueEl.GetProperty("standings")[0];

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbConnection>();

        foreach (var entry in standingsArray.EnumerateArray())
        {
            var teamName = entry.GetProperty("team").GetProperty("name").GetString() ?? "";
            var rank = entry.GetProperty("rank").GetInt32();
            var points = entry.GetProperty("points").GetInt32();
            var all = entry.GetProperty("all");
            var played = all.GetProperty("played").GetInt32();
            var won = all.GetProperty("win").GetInt32();
            var drawn = all.GetProperty("draw").GetInt32();
            var lost = all.GetProperty("lose").GetInt32();
            var goalsFor = all.GetProperty("goals").GetProperty("for").GetInt32();
            var goalsAgainst = all.GetProperty("goals").GetProperty("against").GetInt32();
            var goalDiff = entry.GetProperty("goalsDiff").GetInt32();

            const string sql = """
                INSERT INTO standings
                    (team_name, league, season, rank, points, played, won, drawn, lost, goals_for, goals_against, goal_difference)
                VALUES
                    (@TeamName, @League, @Season, @Rank, @Points, @Played, @Won, @Drawn, @Lost, @GoalsFor, @GoalsAgainst, @GoalDifference)
                ON CONFLICT (team_name, league, season) DO UPDATE
                    SET rank = EXCLUDED.rank,
                        points = EXCLUDED.points,
                        played = EXCLUDED.played,
                        won = EXCLUDED.won,
                        drawn = EXCLUDED.drawn,
                        lost = EXCLUDED.lost,
                        goals_for = EXCLUDED.goals_for,
                        goals_against = EXCLUDED.goals_against,
                        goal_difference = EXCLUDED.goal_difference
                """;

            await db.ExecuteAsync(sql, new
            {
                TeamName = teamName,
                League = leagueName,
                Season = season,
                Rank = rank,
                Points = points,
                Played = played,
                Won = won,
                Drawn = drawn,
                Lost = lost,
                GoalsFor = goalsFor,
                GoalsAgainst = goalsAgainst,
                GoalDifference = goalDiff,
            });
        }
    }
}
