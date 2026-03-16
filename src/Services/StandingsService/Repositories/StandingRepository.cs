using Dapper;
using StandingsService.Models;
using System.Data;

namespace StandingsService.Repositories;

public class StandingRepository : IStandingRepository
{
    private readonly IDbConnection _db;

    public StandingRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Standing>> GetByLeagueAsync(string league, int season)
    {
        const string sql = """
            SELECT * FROM standings
            WHERE league ILIKE @League
            AND season = @Season
            ORDER BY rank ASC
            """;

        return await _db.QueryAsync<Standing>(sql, new { League = league, Season = season });
    }

    public async Task<Standing?> GetByTeamAsync(string teamName, string league, int season)
    {
        const string sql = """
            SELECT * FROM standings
            WHERE team_name ILIKE @TeamName
            AND league ILIKE @League
            AND season = @Season
            """;

        return await _db.QueryFirstOrDefaultAsync<Standing>(sql, new { TeamName = teamName, League = league, Season = season });
    }
}