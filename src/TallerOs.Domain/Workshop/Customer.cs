namespace TallerOs.Domain.Workshop;

public sealed class Customer
{
    private readonly List<Vehicle> vehicles = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Phone { get; private set; } = "";
    public IReadOnlyCollection<Vehicle> Vehicles => vehicles.AsReadOnly();

    private Customer() { }

    public static Customer Create(string? name, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone))
            throw new WorkshopRuleException("El nombre y el teléfono del cliente son obligatorios.");

        return new Customer { Id = Guid.NewGuid(), Name = name.Trim(), Phone = phone.Trim() };
    }
}
