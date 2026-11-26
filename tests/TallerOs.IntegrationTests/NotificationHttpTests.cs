using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Notifications;

namespace TallerOs.IntegrationTests;

public sealed class NotificationHttpTests(WorkshopHttpFixture api) : IClassFixture<WorkshopHttpFixture>
{
    [Fact]
    public async Task RF_NOT_01_Through_07_NotificationsArePrivateAndFollowEmailPreference()
    {
        await Expect("GET", "/api/notifications", null, HttpStatusCode.Unauthorized);
        var rejected = await Submit("standard");
        var requestNotice = Assert.Single(await List("administrator"));
        var secondNotice = Assert.Single(await List("administrator2"));
        Assert.Contains(rejected, requestNotice.Message);
        Assert.Contains(rejected, secondNotice.Message);
        Assert.Empty(await List("standard"));
        Assert.Empty(await List("standard2"));

        var mark = $"/api/notifications/{requestNotice.Id}/read";
        await Expect("PUT", mark, "administrator2", HttpStatusCode.Forbidden);
        var firstRead = await Read("PUT", mark, "administrator");
        Assert.NotEqual(JsonValueKind.Null, firstRead.GetProperty("readUtc").ValueKind);
        var secondRead = await Read("PUT", mark, "administrator");
        Assert.Equal(firstRead.GetProperty("readUtc").GetString(), secondRead.GetProperty("readUtc").GetString());

        var approved = await Submit("standard2");
        await Expect("POST", $"/api/permissions/requests/{rejected}/reject", "administrator",
            HttpStatusCode.OK, """{"reason":"Verificación pendiente"}""");
        await Expect("POST", $"/api/permissions/requests/{approved}/approve", "administrator", HttpStatusCode.OK);
        await Expect("POST", $"/api/permissions/requests/{approved}/approve", "administrator", HttpStatusCode.Conflict);
        Assert.Contains("Verificación pendiente", Assert.Single(await List("standard")).Message);
        Assert.Contains("aprobada", Assert.Single(await List("standard2")).Message);
        await Expect("PUT", "/api/notifications/preferences/email", "administrator2", HttpStatusCode.OK, """{"enabled":false}""");

        var order = await Receive();
        Assert.Contains(await List("administrator"), item => item.Message.Contains(order));
        Assert.Contains(await List("administrator2"), item => item.Message.Contains(order));

        await using var db = OpenDatabase();
        var mail = await db.EmailOutbox.AsNoTracking().ToListAsync();
        Assert.Contains(mail, item => item.Recipient == "administrator@example.test" && item.Body.Contains(order));
        Assert.DoesNotContain(mail, item => item.Recipient == "administrator2@example.test" && item.Body.Contains(order));
        Assert.Contains(mail, item => item.Recipient == "administrator2@example.test" && item.Body.Contains(rejected));
        Assert.DoesNotContain(mail, item => item.Recipient == "disabled@example.test");
    }

    [Fact]
    public async Task NotificationWriteFailureRollsBackOrderAndMail()
    {
        await using var db = OpenDatabase();
        var ordersBefore = await db.WorkOrders.CountAsync();
        var mailBefore = await db.EmailOutbox.CountAsync();
        var vehicle = await CreateVehicle();
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER RejectOrderNotification BEFORE INSERT ON Notifications
            WHEN NEW.EventKey LIKE 'order-received:%'
            BEGIN SELECT RAISE(ABORT, 'test rollback'); END
            """);
        try
        {
            await Expect("POST", "/api/workshop/orders", "standard", HttpStatusCode.InternalServerError,
                JsonSerializer.Serialize(new { vehicleId = vehicle, reason = "Fallo forzado de escritura" }));
            Assert.Equal(ordersBefore, await db.WorkOrders.CountAsync());
            Assert.Equal(mailBefore, await db.EmailOutbox.CountAsync());
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectOrderNotification");
        }
    }

    private async Task<string> Submit(string actor) =>
        (await Read("POST", "/api/permissions/requests", actor, """{"permission":"WorkOrders.Read"}"""))
        .GetProperty("id").GetString()!;

    private async Task<string> CreateVehicle()
    {
        var customer = await Read("POST", "/api/workshop/customers", "standard", """{"name":"Cliente ficticio","phone":"8090000000"}""");
        var payload = new { customerId = customer.GetProperty("id").GetString(), licensePlate = Guid.NewGuid().ToString("N"),
            description = "Vehículo ficticio" };
        return (await Read("POST", "/api/workshop/vehicles", "standard", JsonSerializer.Serialize(payload)))
            .GetProperty("id").GetString()!;
    }

    private async Task<string> Receive()
    {
        var payload = new { vehicleId = await CreateVehicle(), reason = "Recepción ficticia" };
        return (await Read("POST", "/api/workshop/orders", "standard", JsonSerializer.Serialize(payload)))
            .GetProperty("id").GetString()!;
    }

    private async Task<NotificationView[]> List(string actor)
    {
        using var response = await api.Send("GET", "/api/notifications", actor);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NotificationView[]>())!;
    }

    private async Task<JsonElement> Read(string method, string path, string actor, string? data = null)
    {
        using var response = await api.Send(method, path, actor, data);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task Expect(string method, string path, string? actor, HttpStatusCode status, string? data = null)
    {
        using var response = await api.Send(method, path, actor, data);
        Assert.Equal(status, response.StatusCode);
    }

    private AppDbContext OpenDatabase() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={api.DatabasePath};Pooling=False").Options);
}
