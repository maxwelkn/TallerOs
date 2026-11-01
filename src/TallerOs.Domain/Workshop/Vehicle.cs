namespace TallerOs.Domain.Workshop;

public sealed class Vehicle
{
    private readonly List<WorkOrder> workOrders = [];

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    public string LicensePlate { get; private set; } = "";
    public string Description { get; private set; } = "";
    public IReadOnlyCollection<WorkOrder> WorkOrders => workOrders.AsReadOnly();

    private Vehicle() { }

    public static Vehicle Create(Guid customerId, string? licensePlate, string? description)
    {
        if (customerId == Guid.Empty || string.IsNullOrWhiteSpace(licensePlate)
            || string.IsNullOrWhiteSpace(description))
            throw new WorkshopRuleException("El cliente, la placa y la descripción del vehículo son obligatorios.");

        return new Vehicle
        {
            Id = Guid.NewGuid(), CustomerId = customerId,
            LicensePlate = licensePlate.Trim(), Description = description.Trim()
        };
    }
}
