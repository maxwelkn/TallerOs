using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Workshop;
using TallerOs.Domain.Workshop;

namespace TallerOs.Api.Infrastructure.Workshop;

public sealed class WorkOrderStore(AppDbContext db) : IWorkOrderStore
{
    public Task<WorkOrder?> Find(Guid id, CancellationToken cancellationToken) =>
        db.WorkOrders.Include(x => x.Diagnoses).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(WorkOrder order) => db.WorkOrders.Add(order);

    public Task<bool> Save(CancellationToken cancellationToken) => WorkshopPersistence.Save(db, cancellationToken);
}
