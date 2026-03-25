using StandingsService.Models;
using StandingsService.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StandingsService.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class StandingsController : ControllerBase
{
    private readonly IStandingRepository _repository;

    public StandingsController(IStandingRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("{league}/{season}")]
    public async Task<ActionResult<IEnumerable<Standing>>> GetByLeague(string league, int season)
    {
        var standings = await _repository.GetByLeagueAsync(league, season);
        return Ok(standings);
    }

    [HttpGet("{teamName}/{league}/{season}")]
    public async Task<ActionResult<Standing>> GetByTeam(string teamName, string league, int season)
    {
        var standing = await _repository.GetByTeamAsync(teamName, league, season);
        if (standing is null)
            return NotFound();
        return Ok(standing);
    }
}