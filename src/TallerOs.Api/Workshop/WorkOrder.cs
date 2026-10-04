namespace TallerOs.Api.Workshop;

public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public List<Vehicle> Vehicles { get; set; } = [];
}

public sealed class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string LicensePlate { get; set; } = "";
    public string Description { get; set; } = "";
    public List<WorkOrder> WorkOrders { get; set; } = [];
}

public enum WorkOrderStatus { Received, Diagnosed, Authorized, InRepair, Delivered }

public sealed class WorkOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public string Reason { get; set; } = "";
    public WorkOrderStatus Status { get; private set; } = WorkOrderStatus.Received;
    public List<Diagnosis> Diagnoses { get; set; } = [];

    public void MoveTo(WorkOrderStatus next)
    {
        if (!WorkOrderTransitions.IsAllowed(Status, next))
            throw new InvalidOperationException($"Transición de {Status} a {next} no permitida.");
        Status = next;
    }
}

public sealed class Diagnosis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;
    public string Description { get; set; } = "";
}

public static class WorkOrderTransitions
{
    public static readonly IReadOnlySet<(WorkOrderStatus From, WorkOrderStatus To)> Allowed =
        new HashSet<(WorkOrderStatus, WorkOrderStatus)>
        {
            (WorkOrderStatus.Received, WorkOrderStatus.Diagnosed),
            (WorkOrderStatus.Diagnosed, WorkOrderStatus.Authorized),
            (WorkOrderStatus.Authorized, WorkOrderStatus.InRepair),
            (WorkOrderStatus.InRepair, WorkOrderStatus.Delivered)
        };

    public static readonly IReadOnlySet<(WorkOrderStatus From, WorkOrderStatus To)> ExplicitlyForbidden =
        new HashSet<(WorkOrderStatus, WorkOrderStatus)>
        {
            (WorkOrderStatus.Received, WorkOrderStatus.InRepair)
        };

    public static bool IsAllowed(WorkOrderStatus from, WorkOrderStatus to) => Allowed.Contains((from, to));
}
