using TallerOs.Application.Access;
using TallerOs.Application.Common;

namespace TallerOs.Application.Mail;

public sealed record DemoMailSummary(Guid Id, string Subject, IReadOnlyList<string> To);
public sealed record DemoMailMessage(Guid Id, string Subject, IReadOnlyList<string> To, string Text);
public sealed record DemoMailPage(int Total, int Start, int Limit, IReadOnlyList<DemoMailSummary> Messages);
public sealed record DemoMailboxSettings(bool Enabled);
public sealed class DemoMailboxException(string message, Exception? innerException = null) : Exception(message, innerException);

public interface IDemoMailbox
{
    Task<DemoMailPage> List(int start, CancellationToken cancellationToken);
    Task<DemoMailMessage?> Find(Guid id, CancellationToken cancellationToken);
}

public sealed class DemoMailboxService(IDemoMailbox mailbox, ICurrentActor currentActor, DemoMailboxSettings settings)
{
    public Task<Outcome<DemoMailPage>> List(int start = 0, CancellationToken cancellationToken = default) =>
        Execute(async () => start < 0
            ? Outcome<DemoMailPage>.Fail(Failure.Invalid, "El inicio de página no puede ser negativo.")
            : new Outcome<DemoMailPage>(await mailbox.List(start, cancellationToken)));

    public Task<Outcome<DemoMailMessage>> Find(Guid id, CancellationToken cancellationToken = default) =>
        Execute(async () =>
        {
            if (id == Guid.Empty) return Outcome<DemoMailMessage>.Fail(Failure.Invalid, "El identificador no es válido.");
            var message = await mailbox.Find(id, cancellationToken);
            return message is null
                ? Outcome<DemoMailMessage>.Fail(Failure.NotFound, "Correo de demostración no encontrado.")
                : new Outcome<DemoMailMessage>(message);
        });

    private async Task<Outcome<T>> Execute<T>(Func<Task<Outcome<T>>> operation)
    {
        var actor = currentActor.Get();
        if (actor is null || actor.Id == Guid.Empty) return Outcome<T>.Fail(Failure.Unauthorized, "Inicia sesión.");
        if (!actor.IsAdministrator) return Outcome<T>.Fail(Failure.Forbidden, "Se requiere Administrador.");
        if (!settings.Enabled) return Outcome<T>.Fail(Failure.NotFound, "El buzón de demostración está deshabilitado.");
        try { return await operation(); }
        catch (DemoMailboxException)
        {
            return Outcome<T>.Fail(Failure.Unavailable, "El buzón de demostración no está disponible. Intenta más tarde.");
        }
    }
}
