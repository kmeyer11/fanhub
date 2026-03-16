using Dapper;
using RosterService.Models;
using System.Data;

namespace RosterService.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly IDbConnection _db;

    public PlayerRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Player>> GetByTeamAsync(string teamName)
    {
        const string sql = """
            SELECT * FROM players
            WHERE team_name ILIKE @TeamName
            AND status = 'active'
            ORDER BY shirt_number ASC
            """;

        return await _db.QueryAsync<Player>(sql, new { TeamName = teamName });
    }

    public async Task<Player?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM players WHERE id = @Id";
        return await _db.QueryFirstOrDefaultAsync<Player>(sql, new { Id = id });
    }
}