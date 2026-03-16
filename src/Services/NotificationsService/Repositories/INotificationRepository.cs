using NotificationsService.Models;

namespace NotificationsService.Repositories;

public interface INotificationRepository
{
    Task<IEnumerable<Notification>> GetByUserIdAsync(string userId);
    Task AddAsync(Notification notification);
    Task MarkAsReadAsync(int id);
}