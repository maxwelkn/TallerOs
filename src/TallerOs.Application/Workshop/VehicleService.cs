using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Domain.Workshop;

namespace TallerOs.Application.Workshop;

public sealed class VehicleService(IVehicleStore store, ICustomerStore customers, ICurrentActor currentActor)
{
    public async Task<Outcome<VehicleView>> Create(Guid customerId, string? licensePlate, string? description,
        CancellationToken cancellationToken = default)
    {
        var actor = currentActor.Get();
        if (actor is null || actor.Id == Guid.Empty)
            return Outcome<VehicleView>.Fail(Failure.Unauthorized, "Inicia sesión.");

        Vehicle vehicle;
        try { vehicle = Vehicle.Create(customerId, licensePlate, description); }
        catch (WorkshopRuleException error)
        {
            return Outcome<VehicleView>.Fail(Failure.Invalid, error.Message);
        }

        if (!await customers.Exists(customerId, cancellationToken))
            return Outcome<VehicleView>.Fail(Failure.NotFound, "Cliente no encontrado.");
        store.Add(vehicle);
        if (!await store.Save(cancellationToken))
            return Outcome<VehicleView>.Fail(Failure.Conflict, "No se pudo guardar el vehículo. Consulta los datos antes de reintentar.");
        return new(VehicleView.From(vehicle));
    }

    public async Task<Outcome<IReadOnlyList<VehicleView>>> List(CancellationToken cancellationToken = default)
    {
        var actor = currentActor.Get();
        if (actor is null || actor.Id == Guid.Empty)
            return Outcome<IReadOnlyList<VehicleView>>.Fail(Failure.Unauthorized, "Inicia sesión.");
        var vehicles = await store.List(cancellationToken);
        return new(vehicles.Select(VehicleView.From).ToArray());
    }
}
