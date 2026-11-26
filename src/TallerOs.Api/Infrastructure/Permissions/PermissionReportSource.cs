using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Reports;

namespace TallerOs.Api.Infrastructure.Permissions;

public sealed class PermissionReportSource(AppDbContext db) : IPermissionReportSource
{
    public async Task<IReadOnlyList<PermissionRequestSample>> Read(Guid? requesterId, CancellationToken cancellationToken)
    {
        var requests = db.PermissionRequests.AsNoTracking();
        if (requesterId is { } owner) requests = requests.Where(request => request.RequesterId == owner);

        return await requests.Select(request => new PermissionRequestSample(request.CreatedUtc, request.Status.ToString()))
            .ToListAsync(cancellationToken);
    }
}
