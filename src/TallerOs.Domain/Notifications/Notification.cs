namespace TallerOs.Domain.Notifications;

public sealed class Notification
{
    public Guid Id { get; private set; }
    public Guid RecipientId { get; private set; }
    public string EventKey { get; private set; } = "";
    public string Subject { get; private set; } = "";
    public string Message { get; private set; } = "";
    public DateTimeOffset CreatedUtc { get; private set; }
    public DateTimeOffset? ReadUtc { get; private set; }

    private Notification() { }

    public Notification(Guid recipientId, string eventKey, string subject, string message, DateTimeOffset createdUtc)
    {
        Id = Guid.NewGuid();
        RecipientId = recipientId;
        EventKey = eventKey;
        Subject = subject;
        Message = message;
        CreatedUtc = createdUtc;
    }
}

public sealed record NotificationPreference(Guid UserId, bool EmailEnabled);
