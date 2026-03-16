using NotificationsService.Models;
using NotificationsService.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace NotificationsService.Controllers;

[ApiController]
[Route("[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _repository;

    public NotificationsController(INotificationRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<IEnumerable<Notification>>> GetByUserId(string userId)
    {
        var notifications = await _repository.GetByUserIdAsync(userId);
        return Ok(notifications);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        await _repository.MarkAsReadAsync(id);
        return NoContent();
    }
}