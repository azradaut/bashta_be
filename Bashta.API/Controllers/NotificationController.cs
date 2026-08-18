using System.Security.Claims;

using Bashta.Core.DTOs;
using Bashta.Core.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationRepository _notificationRepo;

    public NotificationController(
        INotificationRepository notificationRepo)
    {
        _notificationRepo = notificationRepo;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] bool unreadOnly = false)
    {
        var userId = GetCurrentUserId();

        var notifications =
            await _notificationRepo.GetByUserIdAsync(
                userId,
                unreadOnly);

        var response =
            notifications.Select(n =>
                new NotificationResponse
                {
                    Id = n.Id,
                    Title = n.Title,
                    Body = n.Body,
                    Type = n.Type,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                });

        return Ok(response);
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(
        int id)
    {
        var userId = GetCurrentUserId();

        var updated =
            await _notificationRepo.MarkAsReadAsync(
                id,
                userId);

        if (!updated)
        {
            return NotFound(new
            {
                message = "Notifikacija nije pronađena."
            });
        }

        return NoContent();
    }

    private int GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!int.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException(
                "Korisnički identitet nije dostupan.");
        }

        return userId;
    }

    [HttpDelete("my/read")]
    public async Task<IActionResult> DeleteRead()
    {
        var userId = GetCurrentUserId();
        var deleted = await _notificationRepo.DeleteReadByUserIdAsync(userId);

        return Ok(new { deleted });
    }
}