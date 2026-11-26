using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Notifications;
using TallerOs.Domain.Notifications;

namespace TallerOs.Api.Infrastructure.Notifications;

public sealed class NotificationStore(AppDbContext db) : INotificationStore
{
    public void Add(Notification notification) => db.Notifications.Add(notification);

    public async Task<IReadOnlySet<Guid>> EmailDisabledFor(Guid[] recipients, CancellationToken cancellationToken) =>
        (await db.NotificationPreferences.Where(item => recipients.Contains(item.UserId) && !item.EmailEnabled)
            .Select(item => item.UserId).ToListAsync(cancellationToken)).ToHashSet();

    public async Task<IReadOnlyList<Notification>> List(Guid userId, CancellationToken cancellationToken)
    {
        var items = await db.Notifications.AsNoTracking().Where(item => item.RecipientId == userId)
            .ToListAsync(cancellationToken);
        return items.OrderByDescending(item => item.CreatedUtc).ThenBy(item => item.Id).ToArray();
    }

    public Task<Notification?> Find(Guid id, CancellationToken cancellationToken) =>
        db.Notifications.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<DateTimeOffset?> MarkRead(Guid id, Guid userId, DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        var own = db.Notifications.Where(item => item.Id == id && item.RecipientId == userId);
        await own.Where(item => item.ReadUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.ReadUtc, utcNow), cancellationToken);
        return await own.Select(item => item.ReadUtc).SingleAsync(cancellationToken);
    }

    public Task SetEmail(Guid userId, bool enabled, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO NotificationPreferences (UserId, EmailEnabled) VALUES ({userId}, {enabled})
             ON CONFLICT (UserId) DO UPDATE SET EmailEnabled = excluded.EmailEnabled
             """, cancellationToken);
}
