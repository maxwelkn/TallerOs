using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TallerOs.Application.Workshop;

namespace TallerOs.IntegrationTests;

public sealed class WorkshopHttpTests(WorkshopHttpFixture api) : IClassFixture<WorkshopHttpFixture>
{
    private const string MissingId = "00000000-0000-0000-0000-000000000001";

    public static IEnumerable<object[]> ProtectedRoutes()
    {
        yield return ["POST", "/api/workshop/customers"];
        yield return ["GET", "/api/workshop/customers"];
        yield return ["POST", "/api/workshop/vehicles"];
        yield return ["GET", "/api/workshop/vehicles"];
        yield return ["POST", "/api/workshop/orders"];
        yield return ["GET", "/api/workshop/orders"];
        yield return ["GET", $"/api/workshop/orders/{MissingId}"];
        yield return ["POST", $"/api/workshop/orders/{MissingId}/diagnoses"];
        yield return ["POST", $"/api/workshop/orders/{MissingId}/transitions"];
    }

    public static IEnumerable<object[]> InvalidSessions()
    {
        foreach (var route in ProtectedRoutes())
            foreach (var session in new[] { "unknown", "expired", "revoked", "disabled" })
                yield return [route[0], route[1], session];
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task RF_NEG_06_RouteWithoutSession_Returns401(string method, string path)
    {
        var body = method == "POST" ? "{}" : null;

        using var response = await api.Send(method, path, json: body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(InvalidSessions))]
    public async Task RF_NEG_06_InvalidSession_Returns401(string method, string path, string session)
    {
        var body = method == "POST" ? "{}" : null;

        using var response = await api.Send(method, path, session, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("administrator")]
    [InlineData("reader")]
    public async Task RF_NEG_02_OperationalWorkflow_ReachesDelivered(string role)
    {
        var order = await CreateOrder(role);
        var path = $"/api/workshop/orders/{order.Id}";

        await Deliver(order, role);

        using var details = await api.Send("GET", path, role);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var persisted = await details.Content.ReadFromJsonAsync<WorkOrderDetailsView>();
        Assert.NotNull(persisted);
        Assert.Equal("Delivered", persisted.Status);
        Assert.Equal("Revisar transmisión", Assert.Single(persisted.Diagnoses).Description);
    }

    [Theory]
    [InlineData("administrator")]
    [InlineData("reader")]
    public async Task RF_NEG_06_AllowedReader_CanListOrders(string role)
    {
        var order = await CreateOrder(role);

        using var response = await api.Send("GET", "/api/workshop/orders", role);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<WorkOrderView[]>();
        Assert.Contains(orders!, item => item.Id == order.Id && item.Status == "Received");
    }

    [Fact]
    public async Task RF_NEG_05_DeliveredOrder_RejectsReturnToReceived()
    {
        var order = await CreateOrder("reader");
        await Deliver(order, "reader");
        var path = $"/api/workshop/orders/{order.Id}";

        using var response = await api.Send("POST", $"{path}/transitions", "reader", """{"status":"Received"}""");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var details = await api.Send("GET", path, "reader");
        var persisted = await details.Content.ReadFromJsonAsync<WorkOrderDetailsView>();
        Assert.Equal("Delivered", persisted!.Status);
        Assert.Single(persisted.Diagnoses);
    }

    [Theory]
    [InlineData("/api/workshop/orders")]
    [InlineData("/api/workshop/orders/00000000-0000-0000-0000-000000000001")]
    public async Task RF_NEG_06_StandardWithoutPermission_CannotReadOrders(string path)
    {
        var role = "standard";

        using var response = await api.Send("GET", path, role);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RF_NEG_06_StandardWithoutReadPermission_CanReceiveOrder()
    {
        var role = "standard";

        var order = await CreateOrder(role);

        Assert.Equal("Received", order.Status);
        Assert.Empty(order.Diagnoses);
    }

    [Theory]
    [InlineData("administrator")]
    [InlineData("standard")]
    public async Task RF_NEG_02_OperationalRole_CanListCustomersAndVehicles(string role)
    {
        await CreateOrder(role);

        using var customers = await api.Send("GET", "/api/workshop/customers", role);
        using var vehicles = await api.Send("GET", "/api/workshop/vehicles", role);

        Assert.Equal(HttpStatusCode.OK, customers.StatusCode);
        Assert.Equal(HttpStatusCode.OK, vehicles.StatusCode);
        Assert.NotEmpty((await customers.Content.ReadFromJsonAsync<CustomerView[]>())!);
        Assert.NotEmpty((await vehicles.Content.ReadFromJsonAsync<VehicleView[]>())!);
    }

    [Fact]
    public async Task RF_NEG_06_StandardInvokingAdministratorRoute_Returns403()
    {
        var role = "standard";

        using var response = await api.Send("GET", "/api/admin/users", role);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/workshop/customers", "{}")]
    [InlineData("/api/workshop/customers", "null")]
    [InlineData("/api/workshop/customers", "[]")]
    [InlineData("/api/workshop/customers", "{")]
    [InlineData("/api/workshop/customers", """{"name":7,"phone":"8090000000"}""")]
    [InlineData("/api/workshop/customers", """{"name":" ","phone":"8090000000"}""")]
    [InlineData("/api/workshop/vehicles", "{}")]
    [InlineData("/api/workshop/vehicles", """{"customerId":"incorrecto","licensePlate":"PRUEBA-1","description":"Moto"}""")]
    [InlineData("/api/workshop/orders", "{}")]
    [InlineData("/api/workshop/orders/00000000-0000-0000-0000-000000000001/diagnoses", """{"description":"   "}""")]
    [InlineData("/api/workshop/orders/00000000-0000-0000-0000-000000000001/transitions", """{"status":7}""")]
    [InlineData("/api/workshop/orders/00000000-0000-0000-0000-000000000001/transitions", """{"status":"1"}""")]
    [InlineData("/api/workshop/orders/00000000-0000-0000-0000-000000000001/transitions", """{"status":"Pending"}""")]
    [InlineData("/api/workshop/orders/00000000-0000-0000-0000-000000000001/transitions", """{"status":"Authorized","customerAuthorizationConfirmed":"yes"}""")]
    public async Task RD_07_InvalidBody_ReturnsControlled400(string path, string json)
    {
        var role = "standard";

        using var response = await api.Send("POST", path, role, json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("StackTrace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT ", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/workshop/vehicles", """{"customerId":"00000000-0000-0000-0000-000000000001","licensePlate":"PRUEBA-1","description":"Moto"}""")]
    [InlineData("/api/workshop/orders", """{"vehicleId":"00000000-0000-0000-0000-000000000001","reason":"Ruido"}""")]
    [InlineData("/api/workshop/orders/00000000-0000-0000-0000-000000000001/diagnoses", """{"description":"Revisar"}""")]
    [InlineData("/api/workshop/orders/00000000-0000-0000-0000-000000000001/transitions", """{"status":"Diagnosed"}""")]
    public async Task RD_07_MissingReference_Returns404(string path, string json)
    {
        var role = "standard";

        using var response = await api.Send("POST", path, role, json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RD_07_ReaderRequestingMissingOrder_Returns404()
    {
        var path = $"/api/workshop/orders/{MissingId}";

        using var response = await api.Send("GET", path, "reader");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Diagnosed")]
    [InlineData("InRepair")]
    public async Task RF_NEG_04_TransitionWithMissingConditionOrForbiddenPair_PreservesState(string status)
    {
        var order = await CreateOrder("reader");
        var path = $"/api/workshop/orders/{order.Id}";

        using var response = await api.Send("POST", $"{path}/transitions", "reader",
            JsonSerializer.Serialize(new { status }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var details = await api.Send("GET", path, "reader");
        var persisted = await details.Content.ReadFromJsonAsync<WorkOrderDetailsView>();
        Assert.Equal("Received", persisted!.Status);
        Assert.Empty(persisted.Diagnoses);
    }

    private async Task Deliver(WorkOrderDetailsView order, string role)
    {
        var path = $"/api/workshop/orders/{order.Id}";
        using var diagnosis = await api.Send("POST", $"{path}/diagnoses", role,
            JsonSerializer.Serialize(new { description = "Revisar transmisión" }));
        Assert.Equal(HttpStatusCode.Created, diagnosis.StatusCode);
        foreach (var state in new[] { "Diagnosed", "Authorized", "InRepair", "Delivered" })
        {
            using var transition = await api.Send("POST", $"{path}/transitions", role,
                JsonSerializer.Serialize(new { status = state, customerAuthorizationConfirmed = state == "Authorized",
                    workCompletionConfirmed = state == "Delivered", deliveryConfirmed = state == "Delivered" }));
            Assert.Equal(HttpStatusCode.OK, transition.StatusCode);
        }
    }

    private async Task<WorkOrderDetailsView> CreateOrder(string role)
    {
        using var customerResponse = await api.Send("POST", "/api/workshop/customers", role,
            JsonSerializer.Serialize(new { name = "Cliente ficticio", phone = "8090000000" }));
        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerView>();
        using var vehicleResponse = await api.Send("POST", "/api/workshop/vehicles", role,
            JsonSerializer.Serialize(new { customerId = customer!.Id, licensePlate = $"PRUEBA-{Guid.NewGuid():N}", description = "Moto" }));
        Assert.Equal(HttpStatusCode.Created, vehicleResponse.StatusCode);
        var vehicle = await vehicleResponse.Content.ReadFromJsonAsync<VehicleView>();
        using var orderResponse = await api.Send("POST", "/api/workshop/orders", role,
            JsonSerializer.Serialize(new { vehicleId = vehicle!.Id, reason = "Ruido en el motor" }));
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        var order = await orderResponse.Content.ReadFromJsonAsync<WorkOrderDetailsView>();
        Assert.NotNull(order);
        Assert.Equal($"/api/workshop/orders/{order.Id}", orderResponse.Headers.Location?.ToString());
        return order;
    }
}
