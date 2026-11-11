using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;

namespace TallerOs.Api.Mail;

public sealed class OutboxSender(AppDbContext db, IConfiguration configuration, TimeProvider clock)
{
    public async Task<int> SendPending()
    {
        var settings = new SmtpSettings(configuration);

        var pending = (await db.EmailOutbox.Where(x => x.State == MailState.Pending).ToListAsync())
            .OrderBy(x => x.CreatedUtc).ToList();
        var sent = 0;
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseTls,
            UseDefaultCredentials = false,
            Credentials = settings.UseAuthentication ? new NetworkCredential(settings.Username, settings.Password) : null
        };
        foreach (var item in pending)
        {
            try
            {
                using var message = new MailMessage(settings.From, item.Recipient, item.Subject, item.Body);
                await client.SendMailAsync(message);
                item.State = MailState.Sent;
                item.SentUtc = clock.GetUtcNow();
                item.LastError = null;
                sent++;
            }
            catch (SmtpException ex)
            {
                item.LastError = ex.Message;
            }
            item.Attempts++;
            await db.SaveChangesAsync();
        }
        return sent;
    }
}
