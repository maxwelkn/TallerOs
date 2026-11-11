using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TallerOs.Application.Workshop;

namespace TallerOs.IntegrationTests;

public sealed class DeliveryReviewHttpTests(WorkshopHttpFixture api) : IClassFixture<WorkshopHttpFixture>
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("unknown", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("revoked", HttpStatusCode.Unauthorized)]
    [InlineData("disabled", HttpStatusCode.Unauthorized)]
    [InlineData("standard", HttpStatusCode.Forbidden)]
    public async Task RD_06_ProtectedRouteChecksSessionAndPermission(string? role, HttpStatusCode expected)
    {
        using var response = await api.Send("GET", $"/api/workshop/orders/{Guid.NewGuid()}/delivery-review", role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task MissingOrder_IsNotFound()
    {
        using var response = await api.Send("GET", $"/api/workshop/orders/{Guid.NewGuid()}/delivery-review", "reader");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Received", HttpStatusCode.Conflict)]
    [InlineData("Diagnosed", HttpStatusCode.Conflict)]
    [InlineData("Authorized", HttpStatusCode.Conflict)]
    [InlineData("InRepair", HttpStatusCode.ServiceUnavailable)]
    [InlineData("Delivered", HttpStatusCode.Conflict)]
    public async Task StateAndMissingKey_ProduceControlledResultsWithoutChangingOrder(string state, HttpStatusCode expected)
    {
        var order = await CreateOrder(state);

        using var response = await api.Send("GET", $"/api/workshop/orders/{order.Id}/delivery-review", "reader");

        Assert.Equal(expected, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        using var detail = await api.Send("GET", $"/api/workshop/orders/{order.Id}", "reader");
        Assert.Equal(state, (await detail.Content.ReadFromJsonAsync<WorkOrderDetailsView>())!.Status);
        using var health = await api.Send("GET", "/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        if (state == "InRepair") Assert.Contains("WEATHER_API_KEY", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AdministratorAlsoNeedsConfiguredWeatherAndGetsControlled503()
    {
        var order = await CreateOrder("InRepair");

        using var response = await api.Send("GET", $"/api/workshop/orders/{order.Id}/delivery-review", "administrator");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("https://", await response.Content.ReadAsStringAsync());
    }

    private async Task<WorkOrderDetailsView> CreateOrder(string state)
    {
        using var customerResponse = await api.Send("POST", "/api/workshop/customers", "reader",
            JsonSerializer.Serialize(new { name = "Cliente ficticio", phone = "0000000000" }));
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerView>();
        using var vehicleResponse = await api.Send("POST", "/api/workshop/vehicles", "reader",
            JsonSerializer.Serialize(new { customerId = customer!.Id, licensePlate = $"WEATHER-{Guid.NewGuid():N}", description = "Prueba" }));
        var vehicle = await vehicleResponse.Content.ReadFromJsonAsync<VehicleView>();
        using var orderResponse = await api.Send("POST", "/api/workshop/orders", "reader",
            JsonSerializer.Serialize(new { vehicleId = vehicle!.Id, reason = "Revisión de entrega" }));
        var order = Assert.IsType<WorkOrderDetailsView>(await orderResponse.Content.ReadFromJsonAsync<WorkOrderDetailsView>());
        if (state == "Received") return order;
        using var diagnosis = await api.Send("POST", $"/api/workshop/orders/{order.Id}/diagnoses", "reader",
            JsonSerializer.Serialize(new { description = "Diagnóstico de prueba" }));
        Assert.Equal(HttpStatusCode.Created, diagnosis.StatusCode);
        foreach (var next in new[] { "Diagnosed", "Authorized", "InRepair", "Delivered" })
        {
            using var transition = await api.Send("POST", $"/api/workshop/orders/{order.Id}/transitions", "reader",
                JsonSerializer.Serialize(new { status = next, customerAuthorizationConfirmed = true,
                    workCompletionConfirmed = true, deliveryConfirmed = true }));
            Assert.Equal(HttpStatusCode.OK, transition.StatusCode);
            if (next == state) break;
        }
        return order;
    }
}
