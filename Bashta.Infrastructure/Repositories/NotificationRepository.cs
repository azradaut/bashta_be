using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly BashtaDbContext _context;

    public NotificationRepository(BashtaDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Notification>> GetByUserIdAsync(int userId, bool unreadOnly = false) =>
        await _context.Notifications
            .Where(n => n.UserId == userId && (!unreadOnly || !n.IsRead))
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task<Notification> CreateAsync(Notification notification)
    {
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task MarkAsReadAsync(int id)
    {
        var n = await _context.Notifications.FindAsync(id);
        if (n is not null)
        {
            n.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }
}