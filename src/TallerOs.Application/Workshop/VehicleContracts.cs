using TallerOs.Domain.Workshop;

namespace TallerOs.Application.Workshop;

public sealed record VehicleView(Guid Id, Guid CustomerId, string LicensePlate, string Description)
{
    public static VehicleView From(Vehicle vehicle) =>
        new(vehicle.Id, vehicle.CustomerId, vehicle.LicensePlate, vehicle.Description);
}

public interface IVehicleStore
{
    Task<bool> Exists(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Vehicle>> List(CancellationToken cancellationToken);
    void Add(Vehicle vehicle);
    Task<bool> Save(CancellationToken cancellationToken);
}
