using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Mail;

namespace TallerOs.Api.Mail;

public sealed class MailOutboxReader(AppDbContext db) : IMailOutboxReader
{
    public async Task<IReadOnlyList<MailOutboxEntry>> List(CancellationToken cancellationToken)
    {
        var entries = await db.EmailOutbox.AsNoTracking()
            .Select(item => new MailOutboxEntry(item.Id, item.Recipient, item.Subject, item.State.ToString(),
                item.Attempts, item.CreatedUtc, item.LastAttemptUtc, item.SentUtc, item.LastError))
            .ToListAsync(cancellationToken);
        return entries.OrderByDescending(item => item.CreatedUtc).ThenBy(item => item.Id).ToArray();
    }
}
