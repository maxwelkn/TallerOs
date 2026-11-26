using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;

namespace TallerOs.Api.Mail;

public sealed class OutboxSender(AppDbContext db, IConfiguration configuration, TimeProvider clock)
{
    public const int MaximumAttempts = 3;
    private const string UnconfirmedError = "El intento de SMTP quedó sin confirmar.";

    public async Task<int> SendPending()
    {
        var settings = new SmtpSettings(configuration);
        var lockPath = db.Database.GetDbConnection().DataSource + ".send-mail.lock";
        using var processingLock = AcquireLock(lockPath);
        if (processingLock is null)
            throw new InvalidOperationException("No se pudo reservar el procesador de correo para esta base.");

        var pending = (await db.EmailOutbox.Where(item => item.State == MailState.Pending).ToListAsync())
            .OrderBy(item => item.CreatedUtc).ThenBy(item => item.Id);
        var sent = 0;
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseTls,
            UseDefaultCredentials = false,
            Credentials = settings.UseAuthentication ? new NetworkCredential(settings.Username, settings.Password) : null
        };
        foreach (var item in pending)
        {
            if (item.Attempts >= MaximumAttempts)
            {
                item.State = MailState.Failed;
                item.LastError ??= UnconfirmedError;
                await db.SaveChangesAsync();
                continue;
            }

            item.Attempts++;
            item.LastAttemptUtc = clock.GetUtcNow();
            item.LastError = UnconfirmedError;
            await db.SaveChangesAsync();

            try
            {
                using var message = new MailMessage(settings.From, item.Recipient, item.Subject, item.Body);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await client.SendMailAsync(message, timeout.Token);
                item.State = MailState.Sent;
                item.SentUtc = clock.GetUtcNow();
                item.LastError = null;
                sent++;
            }
            catch (SmtpException error)
            {
                RecordFailure(item, $"Fallo de SMTP ({error.StatusCode}).");
            }
            catch (OperationCanceledException)
            {
                RecordFailure(item, "El servidor SMTP no respondió a tiempo.");
            }
            catch (FormatException)
            {
                RecordFailure(item, "La dirección de correo tiene un formato inválido.");
            }

            await db.SaveChangesAsync();
        }
        return sent;
    }

    private static FileStream? AcquireLock(string path)
    {
        try { return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return null; }
    }

    private static void RecordFailure(QueuedEmail item, string error)
    {
        item.LastError = error;
        if (item.Attempts >= MaximumAttempts)
            item.State = MailState.Failed;
    }
}
