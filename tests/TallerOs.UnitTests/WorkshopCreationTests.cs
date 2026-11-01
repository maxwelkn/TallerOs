using TallerOs.Domain.Workshop;

namespace TallerOs.UnitTests;

public sealed class WorkshopCreationTests
{
    [Theory]
    [InlineData(null, "8090000000")]
    [InlineData("   ", "8090000000")]
    [InlineData("Cliente de prueba", null)]
    [InlineData("Cliente de prueba", "   ")]
    public void RD_07_CreateCustomerWithMissingData_Rejects(string? name, string? phone)
    {
        Action create = () => Customer.Create(name, phone);

        var error = Record.Exception(create);

        Assert.IsType<WorkshopRuleException>(error);
    }

    [Fact]
    public void RD_07_CreateCustomer_NormalizesRequiredData()
    {
        var name = "  Cliente de prueba  ";
        var phone = "  8090000000  ";

        var customer = Customer.Create(name, phone);

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal("Cliente de prueba", customer.Name);
        Assert.Equal("8090000000", customer.Phone);
        Assert.Empty(customer.Vehicles);
    }

    [Fact]
    public void RD_07_CreateVehicleWithoutCustomer_Rejects()
    {
        var customerId = Guid.Empty;

        var error = Record.Exception(() => Vehicle.Create(customerId, "PRUEBA-1", "Motocicleta"));

        Assert.IsType<WorkshopRuleException>(error);
    }

    [Theory]
    [InlineData(null, "Motocicleta")]
    [InlineData("   ", "Motocicleta")]
    [InlineData("PRUEBA-1", null)]
    [InlineData("PRUEBA-1", "   ")]
    public void RD_07_CreateVehicleWithMissingData_Rejects(string? plate, string? description)
    {
        var customerId = Guid.NewGuid();

        var error = Record.Exception(() => Vehicle.Create(customerId, plate, description));

        Assert.IsType<WorkshopRuleException>(error);
    }

    [Fact]
    public void RD_07_CreateVehicle_PreservesCustomerAndNormalizesData()
    {
        var customerId = Guid.NewGuid();

        var vehicle = Vehicle.Create(customerId, "  PRUEBA-1  ", "  Motocicleta  ");

        Assert.NotEqual(Guid.Empty, vehicle.Id);
        Assert.Equal(customerId, vehicle.CustomerId);
        Assert.Equal("PRUEBA-1", vehicle.LicensePlate);
        Assert.Equal("Motocicleta", vehicle.Description);
        Assert.Empty(vehicle.WorkOrders);
    }

    [Fact]
    public void RD_07_CreateOrderWithoutVehicle_Rejects()
    {
        var vehicleId = Guid.Empty;

        var error = Record.Exception(() => WorkOrder.Create(vehicleId, "Ruido"));

        Assert.IsType<WorkshopRuleException>(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RD_07_CreateOrderWithoutReason_Rejects(string? reason)
    {
        var vehicleId = Guid.NewGuid();

        var error = Record.Exception(() => WorkOrder.Create(vehicleId, reason));

        Assert.IsType<WorkshopRuleException>(error);
    }
}
