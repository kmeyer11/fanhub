using Dapper;
using FixturesService.Models;
using System.Data;

namespace FixturesService.Repositories;

public class FixtureRepository : IFixtureRepository
{
    private readonly IDbConnection _db;

    public FixtureRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Fixture>> GetUpcomingByTeamAsync(string teamName)
    {
        const string sql = """
            SELECT * FROM fixtures
            WHERE (home_team = @TeamName OR away_team = @TeamName)
            AND status NOT IN ('FT', 'AET', 'PEN', 'AWD', 'WO')
            ORDER BY match_date ASC
            LIMIT 20
            """;

        return await _db.QueryAsync<Fixture>(sql, new { TeamName = teamName });
    }

    public async Task<IEnumerable<Fixture>> GetHistoryByTeamAsync(string teamName)
    {
        const string sql = """
            SELECT * FROM fixtures
            WHERE (home_team = @TeamName OR away_team = @TeamName)
            AND status IN ('FT', 'AET', 'PEN', 'AWD', 'WO')
            ORDER BY match_date DESC
            LIMIT 20
            """;

        return await _db.QueryAsync<Fixture>(sql, new { TeamName = teamName });
    }

    public async Task<Fixture?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM fixtures WHERE id = @Id";

        return await _db.QueryFirstOrDefaultAsync<Fixture>(sql, new { Id = id });
    }
}