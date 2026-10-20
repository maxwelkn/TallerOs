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
