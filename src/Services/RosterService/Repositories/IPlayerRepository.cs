using RosterService.Models;

namespace RosterService.Repositories;

public interface IPlayerRepository
{
    Task<IEnumerable<Player>> GetByTeamAsync(string teamName);
    Task<Player?> GetByIdAsync(int id);
}