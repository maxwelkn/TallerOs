namespace TallerOs.Domain.Workshop;

public enum WorkOrderStatus { Received, Diagnosed, Authorized, InRepair, Delivered }

public sealed class WorkOrder
{
    private readonly List<Diagnosis> diagnoses = [];

    public Guid Id { get; private set; }
    public Guid VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }
    public string Reason { get; private set; } = "";
    public Guid? ReceivedBy { get; private set; }
    public DateTimeOffset? ReceivedUtc { get; private set; }
    public DateTimeOffset? DeliveredUtc { get; private set; }
    public WorkOrderStatus Status { get; private set; } = WorkOrderStatus.Received;
    public IReadOnlyCollection<Diagnosis> Diagnoses => diagnoses.AsReadOnly();

    private WorkOrder() { }

    public static WorkOrder Create(Guid vehicleId, string? reason)
    {
        if (vehicleId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
            throw new WorkshopRuleException("El vehículo y el motivo de recepción son obligatorios.");

        return new WorkOrder { Id = Guid.NewGuid(), VehicleId = vehicleId, Reason = reason.Trim() };
    }

    public static WorkOrder Receive(Guid vehicleId, string? reason, Guid receivedBy, DateTimeOffset receivedUtc)
    {
        if (receivedBy == Guid.Empty)
            throw new WorkshopRuleException("El responsable de recepción es obligatorio.");
        var order = Create(vehicleId, reason);
        order.ReceivedBy = receivedBy;
        order.ReceivedUtc = receivedUtc.ToUniversalTime();
        return order;
    }

    public Diagnosis AddDiagnosis(string? description)
    {
        var diagnosis = Diagnosis.Create(Id, description);
        diagnoses.Add(diagnosis);
        return diagnosis;
    }

    public void MoveTo(WorkOrderStatus next, bool customerAuthorizationConfirmed = false,
        bool workCompletionConfirmed = false, bool deliveryConfirmed = false, DateTimeOffset? changedUtc = null)
    {
        WorkOrderTransitions.EnsureAllowed(Status, next, diagnoses.Count > 0,
            customerAuthorizationConfirmed, workCompletionConfirmed, deliveryConfirmed);
        if (next == WorkOrderStatus.Delivered)
        {
            if (ReceivedUtc is { } received && (changedUtc is null || changedUtc < received))
                throw new WorkshopRuleException("Indica una fecha de entrega igual o posterior a la recepción.");
            DeliveredUtc = changedUtc?.ToUniversalTime();
        }
        Status = next;
    }
}
