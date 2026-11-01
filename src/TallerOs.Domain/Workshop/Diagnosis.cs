namespace TallerOs.Domain.Workshop;

public sealed class Diagnosis
{
    public Guid Id { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public WorkOrder? WorkOrder { get; private set; }
    public string Description { get; private set; } = "";

    private Diagnosis() { }

    internal static Diagnosis Create(Guid workOrderId, string? description)
    {
        if (workOrderId == Guid.Empty || string.IsNullOrWhiteSpace(description))
            throw new WorkshopRuleException("La orden y la descripción del diagnóstico son obligatorias.");

        return new Diagnosis
        {
            Id = Guid.NewGuid(), WorkOrderId = workOrderId, Description = description.Trim()
        };
    }
}
