using Bashta.Core.DTOs;
using Bashta.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly INotificationRepository _notificationRepo;

    public NotificationController(INotificationRepository notificationRepo)
    {
        _notificationRepo = notificationRepo;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetByUser(int userId, [FromQuery] bool unreadOnly = false)
    {
        var notifications = await _notificationRepo.GetByUserIdAsync(userId, unreadOnly);
        return Ok(notifications.Select(n => new NotificationResponse
        {
            Id = n.Id,
            Title = n.Title,
            Body = n.Body,
            Type = n.Type,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }));
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        await _notificationRepo.MarkAsReadAsync(id);
        return NoContent();
    }
}