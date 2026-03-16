using StandingsService.Models;

namespace StandingsService.Repositories;

public interface IStandingRepository
{
    Task<IEnumerable<Standing>> GetByLeagueAsync(string league, int season);
    Task<Standing?> GetByTeamAsync(string teamName, string league, int season);
}