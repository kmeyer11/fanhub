using TeamService.Models;

namespace TeamService.Repositories;

public interface ITeamRepository
{
    Task<IEnumerable<Team>> GetAllAsync();
    Task<Team?> GetByIdAsync(int id);
    Task<Team?> GetByNameAsync(string name);
}