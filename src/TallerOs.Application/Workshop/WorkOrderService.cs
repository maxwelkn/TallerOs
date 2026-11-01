using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Domain.Workshop;

namespace TallerOs.Application.Workshop;

public sealed class WorkOrderService(IWorkOrderStore store, IVehicleStore vehicles, ICurrentActor currentActor)
{
    public async Task<Outcome<WorkOrderDetailsView>> Receive(Guid vehicleId, string? reason,
        CancellationToken cancellationToken = default)
    {
        var actor = currentActor.Get();
        if (actor is null || actor.Id == Guid.Empty)
            return Outcome<WorkOrderDetailsView>.Fail(Failure.Unauthorized, "Inicia sesión.");

        WorkOrder order;
        try { order = WorkOrder.Create(vehicleId, reason); }
        catch (WorkshopRuleException error)
        {
            return Outcome<WorkOrderDetailsView>.Fail(Failure.Invalid, error.Message);
        }

        if (!await vehicles.Exists(vehicleId, cancellationToken))
            return Outcome<WorkOrderDetailsView>.Fail(Failure.NotFound, "Vehículo no encontrado.");
        store.Add(order);
        if (!await store.Save(cancellationToken)) return Conflict();
        return new(WorkOrderDetailsView.From(order));
    }

    public async Task<Outcome<DiagnosisView>> AddDiagnosis(Guid id, string? description,
        CancellationToken cancellationToken = default)
    {
        var actor = currentActor.Get();
        if (actor is null || actor.Id == Guid.Empty)
            return Outcome<DiagnosisView>.Fail(Failure.Unauthorized, "Inicia sesión.");
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(description))
            return Outcome<DiagnosisView>.Fail(Failure.Invalid, "La orden y la descripción del diagnóstico son obligatorias.");
        var order = await store.Find(id, cancellationToken);
        if (order is null) return Outcome<DiagnosisView>.Fail(Failure.NotFound, "Orden no encontrada.");

        var diagnosis = order.AddDiagnosis(description);
        if (!await store.Save(cancellationToken))
            return Outcome<DiagnosisView>.Fail(Failure.Conflict, "No se pudo guardar el diagnóstico. Consulta la orden antes de reintentar.");
        return new(DiagnosisView.From(diagnosis));
    }

    public async Task<Outcome<WorkOrderDetailsView>> Transition(Guid id, string? status,
        bool customerAuthorizationConfirmed = false, bool workCompletionConfirmed = false,
        bool deliveryConfirmed = false, CancellationToken cancellationToken = default)
    {
        var actor = currentActor.Get();
        if (actor is null || actor.Id == Guid.Empty)
            return Outcome<WorkOrderDetailsView>.Fail(Failure.Unauthorized, "Inicia sesión.");
        var stateName = Enum.GetNames<WorkOrderStatus>().FirstOrDefault(name =>
            string.Equals(name, status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (id == Guid.Empty || stateName is null)
            return Outcome<WorkOrderDetailsView>.Fail(Failure.Invalid, "La orden y el estado solicitado no son válidos.");
        var order = await store.Find(id, cancellationToken);
        if (order is null) return Outcome<WorkOrderDetailsView>.Fail(Failure.NotFound, "Orden no encontrada.");

        try
        {
            order.MoveTo(Enum.Parse<WorkOrderStatus>(stateName), customerAuthorizationConfirmed,
                workCompletionConfirmed, deliveryConfirmed);
        }
        catch (WorkshopRuleException error)
        {
            return Outcome<WorkOrderDetailsView>.Fail(Failure.Conflict, error.Message);
        }

        if (!await store.Save(cancellationToken)) return Conflict();
        return new(WorkOrderDetailsView.From(order));
    }

    private static Outcome<WorkOrderDetailsView> Conflict() =>
        Outcome<WorkOrderDetailsView>.Fail(Failure.Conflict, "La orden cambió o no pudo guardarse. Consúltala antes de reintentar.");
}
