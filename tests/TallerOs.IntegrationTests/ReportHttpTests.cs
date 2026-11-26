using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Reports;
using TallerOs.Domain.Permissions;
using TallerOs.Domain.Workshop;

namespace TallerOs.IntegrationTests;

public sealed class ReportHttpTests(WorkshopHttpFixture api) : IClassFixture<WorkshopHttpFixture>
{
    private const string Permissions = "/api/reports/permission-requests";
    private const string Workshop = "/api/reports/workshop-delivery-times";
    private const string FirstDay = "?from=2026-11-01&to=2026-11-01";
    private const string SecondDay = "?from=2026-11-02&to=2026-11-02";

    [Fact]
    public async Task RF_REP_01_Through_05_ReportsAggregateScopedDataAndHonorDateRange()
    {
        await Seed();
        var first = await Read<PermissionRequestTotal>(Permissions + FirstDay, "administrator");
        Assert.Equal(new PermissionRequestTotal("2026-11", "Pending", 2), Assert.Single(first));
        var second = await Read<PermissionRequestTotal>(Permissions + SecondDay, "administrator");
        Assert.Equal(new PermissionRequestTotal("2026-11", "Rejected", 1), Assert.Single(second));
        var ownPermissions = await Read<PermissionRequestTotal>(Permissions + FirstDay, "standard");
        Assert.Equal(new PermissionRequestTotal("2026-11", "Pending", 1), Assert.Single(ownPermissions));
        Assert.Empty(await Read<PermissionRequestTotal>(Permissions + SecondDay, "standard"));

        var deliveredFirst = await Read<WorkshopDeliveryTime>(Workshop + FirstDay, "administrator");
        Assert.Equal(new WorkshopDeliveryTime("Automobile", 2, 2), Assert.Single(deliveredFirst));
        var deliveredSecond = await Read<WorkshopDeliveryTime>(Workshop + SecondDay, "administrator");
        Assert.Equal(new WorkshopDeliveryTime("Motorcycle", 1, 5), Assert.Single(deliveredSecond));
        var ownDeliveries = await Read<WorkshopDeliveryTime>(Workshop + FirstDay, "standard");
        Assert.Equal(new WorkshopDeliveryTime("Automobile", 1, 1), Assert.Single(ownDeliveries));
        Assert.Empty(await Read<WorkshopDeliveryTime>(Workshop + SecondDay, "standard"));
    }

    [Theory]
    [InlineData(Permissions)]
    [InlineData(Workshop)]
    public async Task ReportsRequireASessionAndExactOrderedDates(string path)
    {
        using var anonymous = await api.Send("GET", path + FirstDay);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        foreach (var suffix in new[] { "", "?from=2026-11-02&to=2026-11-01", "?from=2026-11-1&to=2026-11-02" })
        {
            using var malformed = await api.Send("GET", path + suffix, "standard");
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        }

        Assert.Empty(await Read<object>(path + "?from=9999-12-31&to=9999-12-31", "standard"));
    }

    private async Task<T[]> Read<T>(string path, string actor)
    {
        using var response = await api.Send("GET", path, actor);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T[]>())!;
    }

    private async Task Seed()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={api.DatabasePath};Pooling=False").Options);
        var standard = await UserId(db, "standard");
        var other = await UserId(db, "standard2");
        var admin = await UserId(db, "administrator");
        var first = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var second = first.AddDays(1);

        var rejected = PermissionRequest.Create(other, "WorkOrders.Read", second);
        rejected.Reject(admin, second.AddHours(1), "Motivo de prueba");
        db.PermissionRequests.AddRange(PermissionRequest.Create(standard, "WorkOrders.Read", first),
            PermissionRequest.Create(other, "WorkOrders.Read", second.AddTicks(-1)), rejected);

        var customer = Customer.Create("Cliente ficticio", "8090000000");
        var auto = Vehicle.Create(customer.Id, "PRUEBA-A", "Automóvil", VehicleType.Automobile);
        var moto = Vehicle.Create(customer.Id, "PRUEBA-M", "Motocicleta", VehicleType.Motorcycle);
        db.Customers.Add(customer);
        db.Vehicles.AddRange(auto, moto);
        db.WorkOrders.AddRange(
            Deliver(WorkOrder.Receive(auto.Id, "Uno", standard, first.AddHours(-1)), first),
            Deliver(WorkOrder.Receive(auto.Id, "Dos", other, second.AddTicks(-1).AddHours(-3)), second.AddTicks(-1)),
            Deliver(WorkOrder.Receive(moto.Id, "Tres", other, second.AddHours(-5)), second),
            Deliver(WorkOrder.Create(auto.Id, "Sin fecha de recepción"), first));
        await db.SaveChangesAsync();
    }

    private static Task<Guid> UserId(AppDbContext db, string name) =>
        db.Users.Where(user => user.Email == name + "@example.test").Select(user => user.Id).SingleAsync();

    private static WorkOrder Deliver(WorkOrder order, DateTimeOffset at)
    {
        order.AddDiagnosis("Diagnóstico ficticio");
        order.MoveTo(WorkOrderStatus.Diagnosed);
        order.MoveTo(WorkOrderStatus.Authorized, customerAuthorizationConfirmed: true);
        order.MoveTo(WorkOrderStatus.InRepair);
        order.MoveTo(WorkOrderStatus.Delivered, workCompletionConfirmed: true, deliveryConfirmed: true, changedUtc: at);
        return order;
    }
}
