using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Workshop;
using TallerOs.Domain.Workshop;

namespace TallerOs.Api.Infrastructure.Workshop;

public sealed class VehicleStore(AppDbContext db) : IVehicleStore
{
    public Task<bool> Exists(Guid id, CancellationToken cancellationToken) =>
        db.Vehicles.AnyAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> List(CancellationToken cancellationToken) =>
        await db.Vehicles.AsNoTracking().OrderBy(x => x.LicensePlate).ThenBy(x => x.Id).ToListAsync(cancellationToken);

    public void Add(Vehicle vehicle) => db.Vehicles.Add(vehicle);

    public Task<bool> Save(CancellationToken cancellationToken) => WorkshopPersistence.Save(db, cancellationToken);
}
