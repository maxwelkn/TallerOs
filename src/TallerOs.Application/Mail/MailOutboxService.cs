using TallerOs.Application.Access;
using TallerOs.Application.Common;

namespace TallerOs.Application.Mail;

public sealed record MailOutboxEntry(Guid Id, string Recipient, string Subject, string State, int Attempts,
    DateTimeOffset CreatedUtc, DateTimeOffset? LastAttemptUtc, DateTimeOffset? SentUtc, string? LastError);

public interface IMailOutboxReader
{
    Task<IReadOnlyList<MailOutboxEntry>> List(CancellationToken cancellationToken);
}

public sealed class MailOutboxService(IMailOutboxReader reader, ICurrentActor currentActor)
{
    public async Task<Outcome<IReadOnlyList<MailOutboxEntry>>> List(CancellationToken cancellationToken)
    {
        var actor = currentActor.Get();
        if (actor is null)
            return Outcome<IReadOnlyList<MailOutboxEntry>>.Fail(Failure.Unauthorized, "Inicia sesión.");
        if (!actor.IsAdministrator)
            return Outcome<IReadOnlyList<MailOutboxEntry>>.Fail(Failure.Forbidden, "Se requiere Administrador.");

        return new(await reader.List(cancellationToken));
    }
}
