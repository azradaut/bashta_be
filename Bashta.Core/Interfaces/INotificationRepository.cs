using Bashta.Core.Entities;

namespace Bashta.Core.Interfaces;

public interface INotificationRepository
{
    Task<IEnumerable<Notification>> GetByUserIdAsync(
        int userId,
        bool unreadOnly = false);

    Task<Notification> CreateAsync(
        Notification notification);

    Task<bool> MarkAsReadAsync(
        int id,
        int userId);
}