using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly BashtaDbContext _context;

    public NotificationRepository(
        BashtaDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Notification>> GetByUserIdAsync(
        int userId,
        bool unreadOnly = false)
    {
        return await _context.Notifications
            .Where(n =>
                n.UserId == userId &&
                (!unreadOnly || !n.IsRead))
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<Notification> CreateAsync(
        Notification notification)
    {
        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync();

        return notification;
    }

    public async Task<bool> MarkAsReadAsync(
        int id,
        int userId)
    {
        var notification =
            await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.Id == id &&
                    n.UserId == userId);

        if (notification is null)
            return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;

            await _context.SaveChangesAsync();
        }

        return true;
    }
}