using System.Net;

namespace TallerOs.IntegrationTests;

public sealed class DemoMailboxHttpFixture() : WorkshopHttpFixture(true);

public sealed class DemoMailboxHttpTests(DemoMailboxHttpFixture api) : IClassFixture<DemoMailboxHttpFixture>
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("standard", HttpStatusCode.Forbidden)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("revoked", HttpStatusCode.Unauthorized)]
    [InlineData("disabled", HttpStatusCode.Unauthorized)]
    [InlineData("unknown", HttpStatusCode.Unauthorized)]
    public async Task RD_06_ListAndDetailAreAdministrative(string? role, HttpStatusCode status)
    {
        foreach (var route in new[] { "/api/admin/demo-mail", $"/api/admin/demo-mail/{Guid.NewGuid()}" })
        {
            using var response = await api.Send("GET", route, role);
            Assert.Equal(status, response.StatusCode);
        }
    }

    [Fact]
    public async Task RD_08_UnavailableMailbox_IsControlledAndDoesNotStopApi()
    {
        using var response = await api.Send("GET", "/api/admin/demo-mail", "administrator");
        using var health = await api.Send("GET", "/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.DoesNotContain("127.0.0.1", await response.Content.ReadAsStringAsync());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task RD_07_NegativePage_IsRejected()
    {
        using var response = await api.Send("GET", "/api/admin/demo-mail?start=-1", "administrator");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class DisabledDemoMailboxHttpTests(WorkshopHttpFixture api) : IClassFixture<WorkshopHttpFixture>
{
    [Theory]
    [InlineData(null)]
    [InlineData("standard")]
    [InlineData("administrator")]
    public async Task DemoDisabled_DoesNotMapMailboxRoutes(string? role)
    {
        foreach (var route in new[] { "/api/admin/demo-mail", $"/api/admin/demo-mail/{Guid.NewGuid()}" })
        {
            using var response = await api.Send("GET", route, role);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
