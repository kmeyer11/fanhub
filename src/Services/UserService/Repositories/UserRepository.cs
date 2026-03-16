using Dapper;
using UserService.Models;
using System.Data;

namespace UserService.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IDbConnection _db;

    public UserRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = "SELECT * FROM users WHERE email = @Email";
        return await _db.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM users WHERE id = @Id";
        return await _db.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task AddAsync(User user)
    {
        const string sql = """
            INSERT INTO users (email, password_hash, favorite_team, created_at)
            VALUES (@Email, @PasswordHash, @FavoriteTeam, @CreatedAt)
            """;
        await _db.ExecuteAsync(sql, user);
    }

    public async Task UpdateFavoriteTeamAsync(int id, string teamName)
    {
        const string sql = "UPDATE users SET favorite_team = @TeamName WHERE id = @Id";
        await _db.ExecuteAsync(sql, new { TeamName = teamName, Id = id });
    }
}