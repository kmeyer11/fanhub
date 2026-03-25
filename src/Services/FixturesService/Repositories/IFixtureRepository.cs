using FixturesService.Models;

namespace FixturesService.Repositories;

public interface IFixtureRepository
{
    Task<IEnumerable<Fixture>> GetUpcomingByTeamAsync(string teamName);
    Task<IEnumerable<Fixture>> GetHistoryByTeamAsync(string teamName);
    Task<Fixture?> GetByIdAsync(int id);
}