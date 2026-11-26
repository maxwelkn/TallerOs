using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Access;

namespace TallerOs.Api.Infrastructure.Access;

public sealed class NotificationRecipients(AppDbContext db) : INotificationRecipients
{
    public async Task<IReadOnlyList<NotificationRecipient>> ActiveAdministrators(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(user => user.Role == UserRole.Administrator && user.IsEnabled && user.EmailConfirmed)
            .Select(user => new NotificationRecipient(user.Id, user.Email))
            .ToListAsync(cancellationToken);

    public Task<NotificationRecipient?> Find(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => new NotificationRecipient(user.Id, user.Email))
            .SingleOrDefaultAsync(cancellationToken);
}
