using Dapper;
using NotificationsService.Models;
using System.Data;

namespace NotificationsService.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly IDbConnection _db;

    public NotificationRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Notification>> GetByUserIdAsync(string userId)
    {
        const string sql = """
            SELECT * FROM notifications
            WHERE user_id = @UserId
            ORDER BY created_at DESC
            """;

        return await _db.QueryAsync<Notification>(sql, new { UserId = userId });
    }

    public async Task AddAsync(Notification notification)
    {
        const string sql = """
            INSERT INTO notifications (user_id, title, message, type, is_read, created_at)
            VALUES (@UserId, @Title, @Message, @Type, @IsRead, @CreatedAt)
            """;

        await _db.ExecuteAsync(sql, notification);
    }

    public async Task MarkAsReadAsync(int id)
    {
        const string sql = "UPDATE notifications SET is_read = true WHERE id = @Id";
        await _db.ExecuteAsync(sql, new { Id = id });
    }
}