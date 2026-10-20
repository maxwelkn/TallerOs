using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Permissions;
using TallerOs.Domain.Permissions;

namespace TallerOs.Api.Infrastructure.Permissions;

public sealed class PermissionRequestStore(AppDbContext db) : IPermissionRequestStore
{
    public Task<PermissionRequest?> Find(Guid id, CancellationToken cancellationToken) =>
        db.PermissionRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PermissionRequest>> List(Guid? requesterId, bool pendingOnly,
        CancellationToken cancellationToken)
    {
        var query = db.PermissionRequests.AsNoTracking();
        if (requesterId is not null) query = query.Where(x => x.RequesterId == requesterId);
        if (pendingOnly) query = query.Where(x => x.Status == PermissionRequestStatus.Pending);
        var requests = await query.ToListAsync(cancellationToken);
        return requests.OrderBy(x => x.CreatedUtc).ThenBy(x => x.Id).ToArray();
    }

    public void Add(PermissionRequest request) => db.PermissionRequests.Add(request);

    public async Task<bool> Save(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        catch (DbUpdateException error) when (error.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            return false;
        }
    }
}
