using TallerOs.Api.Data;
using TallerOs.Application.Notifications;

namespace TallerOs.Api.Mail;

public sealed class EmailQueue(AppDbContext db, TimeProvider clock) : IEmailQueue
{
    public void Stage(string recipient, string subject, string message) =>
        db.EmailOutbox.Add(new QueuedEmail
        {
            Recipient = recipient,
            Subject = subject,
            Body = message,
            CreatedUtc = clock.GetUtcNow()
        });
}
