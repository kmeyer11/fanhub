using System.Data;
using Dapper;
using FixturesService.Models;
using System.Text.Json;
using FixturesService.Messaging;

namespace FixturesService.Sync;

public class FixturesSyncService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FixturesSyncService> _logger;
    private readonly HttpClient _httpClient;

    private static readonly int[] TeamLeagues = { 39, 119, 140 };
    private static readonly int[] FixtureLeagues = { 39, 119, 140, 2 };
    public FixturesSyncService(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<FixturesSyncService> logger,
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
                _logger.LogInformation("Starting fixtures sync...");
                await SyncAllLeaguesAsync();
                _logger.LogInformation("Fixtures sync complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during fixtures sync.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task SyncAllLeaguesAsync()
    {
        var apiKey = _config["ApiFootball:ApiKey"];
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-apisports-key", apiKey);

        var season = await ResolveLatestSupportedSeasonAsync();
        _logger.LogInformation("Using season {Season} for fixtures sync.", season);

        foreach (var leagueId in FixtureLeagues)
        {
            try
            {
                _logger.LogInformation("Syncing fixtures for league {LeagueId}...", leagueId);
                await SyncLeagueFixturesAsync(leagueId, season);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing league {LeagueId}.", leagueId);
            }
        }
    }

    private async Task<int> ResolveLatestSupportedSeasonAsync()
    {
        var candidate = DateTime.UtcNow.Month >= 7 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;
        for (var season = candidate; season >= candidate - 3; season--)
        {
            var probe = await _httpClient.GetAsync(
                $"https://v3.football.api-sports.io/fixtures?league=39&season={season}&last=1");
            var doc = JsonDocument.Parse(await probe.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("results", out var r) && r.GetInt32() > 0)
                return season;
        }
        return candidate - 1;
    }

    private async Task SyncLeagueFixturesAsync(int leagueId, int season)
    {
        var response = await _httpClient.GetAsync(
            $"https://v3.football.api-sports.io/fixtures?league={leagueId}&season={season}");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var fixturesArray = doc.RootElement.GetProperty("response");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbConnection>();
        var publisher = scope.ServiceProvider.GetRequiredService<FixtureResultPublisher>();

        foreach (var item in fixturesArray.EnumerateArray())
        {
            var fixtureInfo = item.GetProperty("fixture");
            var teams = item.GetProperty("teams");
            var league = item.GetProperty("league");
            var goals = item.GetProperty("goals");

            var fixture = new Fixture
            {
                HomeTeam = teams.GetProperty("home").GetProperty("name").GetString() ?? "",
                AwayTeam = teams.GetProperty("away").GetProperty("name").GetString() ?? "",
                MatchDate = fixtureInfo.GetProperty("date").GetDateTime(),
                Competition = league.GetProperty("name").GetString() ?? "",
                Venue = fixtureInfo.GetProperty("venue").GetProperty("name").GetString() ?? "",
                HomeScore = goals.GetProperty("home").ValueKind == JsonValueKind.Null ? null : goals.GetProperty("home").GetInt32(),
                AwayScore = goals.GetProperty("away").ValueKind == JsonValueKind.Null ? null : goals.GetProperty("away").GetInt32(),
                Status = fixtureInfo.GetProperty("status").GetProperty("short").GetString() ?? "NS"
            };

            const string sql = """
                INSERT INTO fixtures (home_team, away_team, match_date, competition, venue, home_score, away_score, status)
                VALUES (@HomeTeam, @AwayTeam, @MatchDate, @Competition, @Venue, @HomeScore, @AwayScore, @Status)
                ON CONFLICT DO NOTHING
                RETURNING id
                """;

            var insertedId = await db.ExecuteScalarAsync<int?>(sql, fixture);

            if (insertedId.HasValue && fixture.Status == "FT" && fixture.HomeScore.HasValue && fixture.AwayScore.HasValue)
            {
                await publisher.PublishAsync(new FixtureResultMessage
                {
                    HomeTeam = fixture.HomeTeam,
                    AwayTeam = fixture.AwayTeam,
                    HomeScore = fixture.HomeScore.Value,
                    AwayScore = fixture.AwayScore.Value,
                    Competition = fixture.Competition
                });
            }
        }
    }
}