using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;

namespace TallerOs.Api.Mail;

public sealed class OutboxSender(AppDbContext db, IConfiguration configuration, TimeProvider clock)
{
    public async Task<int> SendPending()
    {
        var host = configuration["SMTP_HOST"];
        var username = configuration["SMTP_USER"];
        var password = configuration["SMTP_PASSWORD"];
        var from = configuration["SMTP_FROM"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Configura SMTP_HOST, SMTP_USER, SMTP_PASSWORD y SMTP_FROM.");
        if (!int.TryParse(configuration["SMTP_PORT"], out var port)) port = 587;

        var pending = (await db.EmailOutbox.Where(x => x.State == MailState.Pending).ToListAsync())
            .OrderBy(x => x.CreatedUtc).ToList();
        var sent = 0;
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(username, password)
        };
        foreach (var item in pending)
        {
            try
            {
                using var message = new MailMessage(from, item.Recipient, item.Subject, item.Body);
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
