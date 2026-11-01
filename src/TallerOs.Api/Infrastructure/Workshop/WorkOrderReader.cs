using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Workshop;

namespace TallerOs.Api.Infrastructure.Workshop;

public sealed class WorkOrderReader(AppDbContext db) : IWorkOrderReader
{
    public async Task<IReadOnlyList<WorkOrderView>> List(CancellationToken cancellationToken) =>
        await db.WorkOrders.AsNoTracking().OrderBy(x => x.Id).Select(x =>
            new WorkOrderView(x.Id, x.VehicleId, x.Reason, x.Status.ToString())).ToListAsync(cancellationToken);

    public async Task<WorkOrderDetailsView?> Find(Guid id, CancellationToken cancellationToken)
    {
        var order = await db.WorkOrders.AsNoTracking().Include(x => x.Diagnoses)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return order is null ? null : WorkOrderDetailsView.From(order);
    }
}
