using RosterService.Models;
using RosterService.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RosterService.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class RosterController : ControllerBase
{
    private readonly IPlayerRepository _repository;

    public RosterController(IPlayerRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("{teamName}")]
    public async Task<ActionResult<IEnumerable<Player>>> GetByTeam(string teamName)
    {
        var players = await _repository.GetByTeamAsync(teamName);
        return Ok(players);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Player>> GetById(int id)
    {
        var player = await _repository.GetByIdAsync(id);
        if (player is null)
            return NotFound();
        return Ok(player);
    }
}