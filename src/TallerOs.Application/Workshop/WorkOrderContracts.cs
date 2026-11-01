using TallerOs.Domain.Workshop;

namespace TallerOs.Application.Workshop;

public sealed record DiagnosisView(Guid Id, Guid WorkOrderId, string Description)
{
    public static DiagnosisView From(Diagnosis diagnosis) =>
        new(diagnosis.Id, diagnosis.WorkOrderId, diagnosis.Description);
}

public sealed record WorkOrderDetailsView(Guid Id, Guid VehicleId, string Reason, string Status,
    IReadOnlyList<DiagnosisView> Diagnoses)
{
    public static WorkOrderDetailsView From(WorkOrder order) => new(order.Id, order.VehicleId, order.Reason,
        order.Status.ToString(), order.Diagnoses.OrderBy(x => x.Id).Select(DiagnosisView.From).ToArray());
}

public interface IWorkOrderStore
{
    Task<WorkOrder?> Find(Guid id, CancellationToken cancellationToken);
    void Add(WorkOrder order);
    Task<bool> Save(CancellationToken cancellationToken);
}
