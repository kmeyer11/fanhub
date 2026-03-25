using System.Data;
using System.Text.Json;
using Dapper;

namespace RosterService.Sync;

public class RosterSyncService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RosterSyncService> _logger;
    private readonly HttpClient _httpClient;

    private static readonly Dictionary<int, string> LeagueTeamIds = new()
    {
        { 39,  "Premier League" },
        { 119, "Superliga"      },
        { 140, "La Liga"        },
    };

    public RosterSyncService(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<RosterSyncService> logger,
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
                _logger.LogInformation("Starting roster sync...");
                await SyncAllLeaguesAsync();
                _logger.LogInformation("Roster sync complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during roster sync.");
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
        _logger.LogInformation("Using season {Season} for roster sync.", season);

        foreach (var (leagueId, leagueName) in LeagueTeamIds)
        {
            try
            {
                _logger.LogInformation("Syncing roster for league {LeagueId}...", leagueId);
                await SyncLeagueRosterAsync(leagueId, season);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing roster for league {LeagueId}.", leagueId);
            }
        }
    }

    private async Task<int> ResolveLatestSupportedSeasonAsync()
    {
        var candidate = DateTime.UtcNow.Month >= 7 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;
        for (var season = candidate; season >= candidate - 3; season--)
        {
            var probe = await _httpClient.GetAsync(
                $"https://v3.football.api-sports.io/players?league=39&season={season}&page=1");
            var doc = JsonDocument.Parse(await probe.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("results", out var r) && r.GetInt32() > 0)
                return season;
        }
        return candidate - 1;
    }

    private async Task SyncLeagueRosterAsync(int leagueId, int season)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbConnection>();

        var page = 1;
        while (true)
        {
            var response = await _httpClient.GetAsync(
                $"https://v3.football.api-sports.io/players?league={leagueId}&season={season}&page={page}");
            response.EnsureSuccessStatusCode();

            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = doc.RootElement.GetProperty("response");
            var paging = doc.RootElement.GetProperty("paging");
            var totalPages = paging.GetProperty("total").GetInt32();

            foreach (var item in items.EnumerateArray())
            {
                var player = item.GetProperty("player");
                var stats = item.GetProperty("statistics");
                if (stats.GetArrayLength() == 0) continue;
                var stat = stats[0];
                var teamEl = stat.GetProperty("team");

                var name = player.GetProperty("name").GetString() ?? "";
                var teamName = teamEl.GetProperty("name").GetString() ?? "";
                var nationality = player.GetProperty("nationality").GetString() ?? "";
                var position = stat.GetProperty("games").GetProperty("position").GetString() ?? "Unknown";
                var shirtNumber = player.TryGetProperty("number", out var num) && num.ValueKind == JsonValueKind.Number
                    ? num.GetInt32() : 0;
                var dobStr = player.GetProperty("birth").GetProperty("date").GetString();
                var dob = DateOnly.TryParse(dobStr, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);

                const string sql = """
                    INSERT INTO players (name, position, nationality, shirt_number, date_of_birth, team_name, status)
                    VALUES (@Name, @Position, @Nationality, @ShirtNumber, @DateOfBirth, @TeamName, 'active')
                    ON CONFLICT (name, team_name) DO UPDATE
                        SET position = EXCLUDED.position,
                            nationality = EXCLUDED.nationality,
                            shirt_number = EXCLUDED.shirt_number
                    """;

                await db.ExecuteAsync(sql, new
                {
                    Name = name,
                    Position = position,
                    Nationality = nationality,
                    ShirtNumber = shirtNumber,
                    DateOfBirth = dob.ToDateTime(TimeOnly.MinValue),
                    TeamName = teamName,
                });
            }

            if (page >= totalPages) break;
            page++;

            // Respect free tier rate limit
            await Task.Delay(200);
        }
    }
}
