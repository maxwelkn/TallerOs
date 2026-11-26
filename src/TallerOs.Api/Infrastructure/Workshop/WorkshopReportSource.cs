using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Reports;
using TallerOs.Domain.Workshop;

namespace TallerOs.Api.Infrastructure.Workshop;

public sealed class WorkshopReportSource(AppDbContext db) : IWorkshopReportSource
{
    public async Task<IReadOnlyList<DeliveryTimeSample>> Read(Guid? receivedBy, CancellationToken cancellationToken)
    {
        var orders = db.WorkOrders.AsNoTracking().Where(order => order.Status == WorkOrderStatus.Delivered
            && order.ReceivedUtc != null && order.DeliveredUtc != null);
        if (receivedBy is { } owner) orders = orders.Where(order => order.ReceivedBy == owner);

        return await orders.Join(db.Vehicles.AsNoTracking(), order => order.VehicleId, vehicle => vehicle.Id,
                (order, vehicle) => new { order.ReceivedUtc, order.DeliveredUtc, vehicle.Type })
            .Select(row => new DeliveryTimeSample(row.DeliveredUtc!.Value, row.ReceivedUtc!.Value, row.Type.ToString()))
            .ToListAsync(cancellationToken);
    }
}
