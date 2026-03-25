using Dapper;
using TeamService.Models;
using System.Data;

namespace TeamService.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly IDbConnection _db;

    public TeamRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Team>> GetAllAsync()
    {
        const string sql = "SELECT * FROM teams ORDER BY league ASC, name ASC";
        return await _db.QueryAsync<Team>(sql);
    }

    public async Task<Team?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM teams WHERE id = @Id";
        return await _db.QueryFirstOrDefaultAsync<Team>(sql, new { Id = id });
    }

    public async Task<Team?> GetByNameAsync(string name)
    {
        const string sql = "SELECT * FROM teams WHERE name ILIKE @Name";
        return await _db.QueryFirstOrDefaultAsync<Team>(sql, new { Name = name });
    }
}