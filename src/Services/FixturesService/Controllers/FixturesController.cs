using FixturesService.Models;
using FixturesService.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixturesService.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class FixturesController : ControllerBase
{
    private readonly IFixtureRepository _repository;

    public FixturesController(IFixtureRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("{teamName}/upcoming")]
    public async Task<ActionResult<IEnumerable<Fixture>>> GetUpcoming(string teamName)
    {
        var fixtures = await _repository.GetUpcomingByTeamAsync(teamName);
        return Ok(fixtures);
    }

    [HttpGet("{teamName}/history")]
    public async Task<ActionResult<IEnumerable<Fixture>>> GetHistory(string teamName)
    {
        var fixtures = await _repository.GetHistoryByTeamAsync(teamName);
        return Ok(fixtures);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Fixture>> GetById(int id)
    {
        var fixture = await _repository.GetByIdAsync(id);
        if (fixture is null)
            return NotFound();

        return Ok(fixture);
    }
}