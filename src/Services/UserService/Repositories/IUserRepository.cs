using UserService.Models;

namespace UserService.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int id);
    Task AddAsync(User user);
    Task UpdateFavoriteTeamAsync(int id, string teamName);
    Task<IEnumerable<int>> GetIdsByFavoriteTeamAsync(string teamName);
}