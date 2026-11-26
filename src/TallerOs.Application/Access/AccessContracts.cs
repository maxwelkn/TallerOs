namespace TallerOs.Application.Access;

public sealed record Actor(Guid Id, bool IsAdministrator);

public interface ICurrentActor
{
    Actor? Get();
}

public interface IAccessPermissions
{
    Task<bool> HasPermission(Guid userId, string permission, CancellationToken cancellationToken);
    Task StageGrant(Guid userId, string permission, CancellationToken cancellationToken);
}

public interface IPermissionCatalog
{
    bool Contains(string permission);
}

public sealed record NotificationRecipient(Guid Id, string Email);

public interface INotificationRecipients
{
    Task<IReadOnlyList<NotificationRecipient>> ActiveAdministrators(CancellationToken cancellationToken);
    Task<NotificationRecipient?> Find(Guid userId, CancellationToken cancellationToken);
}
