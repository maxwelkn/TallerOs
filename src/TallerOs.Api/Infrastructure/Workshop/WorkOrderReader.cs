using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Workshop;

namespace TallerOs.Api.Infrastructure.Workshop;

public sealed class WorkOrderReader(AppDbContext db) : IWorkOrderReader
{
    public async Task<IReadOnlyList<WorkOrderView>> List(CancellationToken cancellationToken) =>
        await db.WorkOrders.AsNoTracking().OrderBy(x => x.Id).Select(x =>
            new WorkOrderView(x.Id, x.VehicleId, x.Reason, x.Status.ToString())).ToListAsync(cancellationToken);
}
