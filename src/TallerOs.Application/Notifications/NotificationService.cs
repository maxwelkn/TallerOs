using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Domain.Notifications;

namespace TallerOs.Application.Notifications;

public interface INotificationPublisher
{
    Task StageForAdministrators(string eventKey, string subject, string message, CancellationToken cancellationToken);
    Task StageForUser(Guid userId, string eventKey, string subject, string message, CancellationToken cancellationToken);
}

public interface INotificationStore
{
    void Add(Notification notification);
    Task<IReadOnlySet<Guid>> EmailDisabledFor(Guid[] recipients, CancellationToken cancellationToken);
    Task<IReadOnlyList<Notification>> List(Guid userId, CancellationToken cancellationToken);
    Task<Notification?> Find(Guid id, CancellationToken cancellationToken);
    Task<DateTimeOffset?> MarkRead(Guid id, Guid userId, DateTimeOffset utcNow, CancellationToken cancellationToken);
    Task SetEmail(Guid userId, bool enabled, CancellationToken cancellationToken);
}

public interface IEmailQueue
{
    void Stage(string recipient, string subject, string message);
}

public sealed record NotificationView(Guid Id, string Subject, string Message, DateTimeOffset CreatedUtc, DateTimeOffset? ReadUtc)
{
    public static NotificationView From(Notification notification) =>
        new(notification.Id, notification.Subject, notification.Message, notification.CreatedUtc, notification.ReadUtc);
}

public sealed class NotificationPublisher(INotificationStore store, INotificationRecipients recipients,
    IEmailQueue email, TimeProvider clock) : INotificationPublisher
{
    public async Task StageForAdministrators(string eventKey, string subject, string message, CancellationToken cancellationToken)
    {
        var administrators = await recipients.ActiveAdministrators(cancellationToken);
        await Stage(administrators, eventKey, subject, message, cancellationToken);
    }

    public async Task StageForUser(Guid userId, string eventKey, string subject, string message, CancellationToken cancellationToken)
    {
        var recipient = await recipients.Find(userId, cancellationToken);
        if (recipient is not null)
            await Stage([recipient], eventKey, subject, message, cancellationToken);
    }

    private async Task Stage(IReadOnlyList<NotificationRecipient> targets, string eventKey, string subject,
        string message, CancellationToken cancellationToken)
    {
        if (targets.Count == 0) return;
        var disabled = await store.EmailDisabledFor(targets.Select(target => target.Id).ToArray(), cancellationToken);
        var now = clock.GetUtcNow();
        foreach (var target in targets)
        {
            store.Add(new Notification(target.Id, eventKey, subject, message, now));
            if (!disabled.Contains(target.Id))
                email.Stage(target.Email, subject, message);
        }
    }
}

public sealed class NotificationService(INotificationStore store, ICurrentActor currentActor, TimeProvider clock)
{
    public async Task<Outcome<IReadOnlyList<NotificationView>>> List(CancellationToken cancellationToken)
    {
        var actor = currentActor.Get();
        if (actor is null)
            return Outcome<IReadOnlyList<NotificationView>>.Fail(Failure.Unauthorized, "Inicia sesión.");

        var items = await store.List(actor.Id, cancellationToken);
        return new(items.Select(NotificationView.From).ToArray());
    }

    public async Task<Outcome<NotificationView>> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var actor = currentActor.Get();
        if (actor is null) return Outcome<NotificationView>.Fail(Failure.Unauthorized, "Inicia sesión.");
        var notification = await store.Find(id, cancellationToken);
        if (notification is null)
            return Outcome<NotificationView>.Fail(Failure.NotFound, "Notificación no encontrada.");
        if (notification.RecipientId != actor.Id)
            return Outcome<NotificationView>.Fail(Failure.Forbidden, "La notificación pertenece a otra cuenta.");

        var readUtc = notification.ReadUtc ?? await store.MarkRead(id, actor.Id, clock.GetUtcNow(), cancellationToken);
        return new(NotificationView.From(notification) with { ReadUtc = readUtc });
    }

    public async Task<Outcome<bool>> SetEmail(bool? enabled, CancellationToken cancellationToken)
    {
        var actor = currentActor.Get();
        if (actor is null) return Outcome<bool>.Fail(Failure.Unauthorized, "Inicia sesión.");
        if (enabled is not bool value)
            return Outcome<bool>.Fail(Failure.Invalid, "Indica si el correo está habilitado.");

        await store.SetEmail(actor.Id, value, cancellationToken);
        return new(value);
    }
}
