using TeamService.Models;
using TeamService.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace TeamService.Controllers;

[ApiController]
[Route("[controller]")]
public class TeamsController : ControllerBase
{
    private readonly ITeamRepository _repository;

    public TeamsController(ITeamRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Team>>> GetAll()
    {
        var teams = await _repository.GetAllAsync();
        return Ok(teams);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Team>> GetById(int id)
    {
        var team = await _repository.GetByIdAsync(id);
        if (team is null)
            return NotFound();
        return Ok(team);
    }

    [HttpGet("{name}")]
    public async Task<ActionResult<Team>> GetByName(string name)
    {
        var team = await _repository.GetByNameAsync(name);
        if (team is null)
            return NotFound();
        return Ok(team);
    }
}