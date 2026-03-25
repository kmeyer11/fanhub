using System.Data;
using System.Text.Json;
using Dapper;

namespace TeamService.Sync;

public class TeamSyncService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TeamSyncService> _logger;
    private readonly HttpClient _httpClient;

    private static readonly Dictionary<int, string> LeagueNames = new()
    {
        { 39,  "Premier League" },
        { 119, "Superliga"      },
        { 140, "La Liga"        },
    };

    public TeamSyncService(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<TeamSyncService> logger,
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
                _logger.LogInformation("Starting team sync...");
                await SyncAllLeaguesAsync();
                _logger.LogInformation("Team sync complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during team sync.");
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
        _logger.LogInformation("Using season {Season} for team sync.", season);

        foreach (var (leagueId, leagueName) in LeagueNames)
        {
            try
            {
                _logger.LogInformation("Syncing teams for league {LeagueId}...", leagueId);
                await SyncLeagueTeamsAsync(leagueId, leagueName, season);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing teams for league {LeagueId}.", leagueId);
            }
        }
    }

    private async Task<int> ResolveLatestSupportedSeasonAsync()
    {
        var candidate = DateTime.UtcNow.Month >= 7 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;
        for (var season = candidate; season >= candidate - 3; season--)
        {
            var probe = await _httpClient.GetAsync(
                $"https://v3.football.api-sports.io/teams?league=39&season={season}");
            var doc = JsonDocument.Parse(await probe.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("results", out var r) && r.GetInt32() > 0)
                return season;
        }
        return candidate - 1; // fallback
    }

    private async Task SyncLeagueTeamsAsync(int leagueId, string leagueName, int season)
    {
        var response = await _httpClient.GetAsync(
            $"https://v3.football.api-sports.io/teams?league={leagueId}&season={season}");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("response");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbConnection>();

        foreach (var item in items.EnumerateArray())
        {
            var team  = item.GetProperty("team");
            var venue = item.GetProperty("venue");

            var name      = team.GetProperty("name").GetString()    ?? "";
            var shortName = team.GetProperty("code").GetString()    ?? name[..Math.Min(name.Length, 3)].ToUpper();
            var country   = team.GetProperty("country").GetString() ?? "";
            var founded   = team.TryGetProperty("founded", out var f) && f.ValueKind == JsonValueKind.Number
                            ? f.GetInt32() : 0;
            var badgeUrl  = team.GetProperty("logo").GetString()    ?? "";
            var stadium   = venue.GetProperty("name").GetString()   ?? "";

            const string sql = """
                INSERT INTO teams (name, short_name, country, league, stadium, founded, badge_url)
                VALUES (@Name, @ShortName, @Country, @League, @Stadium, @Founded, @BadgeUrl)
                ON CONFLICT (name) DO UPDATE
                    SET league = EXCLUDED.league,
                        short_name = EXCLUDED.short_name,
                        badge_url = EXCLUDED.badge_url
                """;

            await db.ExecuteAsync(sql, new
            {
                Name      = name,
                ShortName = shortName,
                Country   = country,
                League    = leagueName,
                Stadium   = stadium,
                Founded   = founded,
                BadgeUrl  = badgeUrl,
            });
        }
    }
}
