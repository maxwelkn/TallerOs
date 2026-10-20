using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Access;

namespace TallerOs.Api.Infrastructure.Access;

public sealed class UserPermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Permission { get; set; } = "";
}

public sealed class AccessPermissions(AppDbContext db) : IAccessPermissions
{
    public Task<bool> HasPermission(Guid userId, string permission, CancellationToken cancellationToken) =>
        db.UserPermissions.AnyAsync(x => x.UserId == userId && x.Permission == permission, cancellationToken);

    public async Task StageGrant(Guid userId, string permission, CancellationToken cancellationToken)
    {
        if (!await HasPermission(userId, permission, cancellationToken))
            db.UserPermissions.Add(new UserPermission { UserId = userId, Permission = permission });
    }
}

public sealed class ConfiguredPermissionCatalog(IEnumerable<string> permissions) : IPermissionCatalog
{
    private readonly HashSet<string> allowed = new(permissions, StringComparer.Ordinal);

    public bool Contains(string permission) => allowed.Contains(permission);
}
