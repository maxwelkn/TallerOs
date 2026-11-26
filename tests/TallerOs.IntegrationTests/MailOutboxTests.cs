using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using TallerOs.Api.Data;
using TallerOs.Api.Mail;
using TallerOs.Application.Mail;

namespace TallerOs.IntegrationTests;

public sealed class MailOutboxTests(WorkshopHttpFixture api) : IClassFixture<WorkshopHttpFixture>
{
    [Fact]
    public async Task RF_NOT_08_Through_12_QueueTracksRetriesAndDeterminesWhoCanInspect()
    {
        using var registration = await api.Send("POST", "/api/access/register", json:
            """{"name":"Cuenta ficticia","email":"outbox@example.test","password":"DemoPass2031"}""");
        registration.EnsureSuccessStatusCode();
        using var denied = await api.Send("GET", "/api/admin/mail-outbox", "standard");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var initial = await Inspect("outbox@example.test");
        Assert.Equal(("Pending", 0), (initial.State, initial.Attempts));
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await using var db = Open(api.DatabasePath);
            Assert.Equal(0, await Sender(db, port).SendPending());
            var row = await Inspect("outbox@example.test");
            Assert.Equal(attempt < 3 ? "Pending" : "Failed", row.State);
            Assert.Equal(Math.Min(attempt, 3), row.Attempts);
            Assert.NotNull(row.LastAttemptUtc);
            Assert.StartsWith("Fallo de SMTP", row.LastError);
            Assert.Null(row.SentUtc);
        }

        using var second = await api.Send("POST", "/api/access/register", json:
            """{"name":"Cuenta ficticia","email":"delivery@example.test","password":"DemoPass2031"}""");
        second.EnsureSuccessStatusCode();

        await using (var db = Open(api.DatabasePath))
        using (File.Open(api.DatabasePath + ".send-mail.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Sender(db, port).SendPending());
            Assert.Equal(0, (await Inspect("delivery@example.test")).Attempts);
        }

        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var captured = AcceptMail(listener, timeout.Token);
            await using var db = Open(api.DatabasePath);
            Assert.Equal(1, await Sender(db, port).SendPending());
            Assert.Equal(0, await Sender(db, port).SendPending());
            Assert.Contains("delivery@example.test", await captured);
            var delivered = await Inspect("delivery@example.test");
            Assert.Equal(("Sent", 1), (delivered.State, delivered.Attempts));
            Assert.NotNull(delivered.SentUtc);
            Assert.Null(delivered.LastError);
            Assert.False(listener.Pending());
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task OldQueueEntriesRetainStateAttemptsAndDatesAfterMigration()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrations = db.GetService<IMigrator>();
        await migrations.MigrateAsync("AddNotifications");
        var id = Guid.NewGuid();
        var date = new DateTimeOffset(2026, 11, 4, 10, 30, 0, TimeSpan.Zero);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO EmailOutbox (Id, Recipient, Subject, Body, State, Attempts, CreatedUtc, SentUtc, LastError)
             VALUES ({id}, 'legacy@example.test', 'Demo', 'Demo', 'Sent', 2, {date}, {date}, NULL)
             """);

        await db.Database.MigrateAsync();

        var mail = await db.EmailOutbox.SingleAsync();
        Assert.Equal(id, mail.Id);
        Assert.Equal(MailState.Sent, mail.State);
        Assert.Equal(2, mail.Attempts);
        Assert.Equal(date, mail.CreatedUtc);
        Assert.Equal(date, mail.SentUtc);
        Assert.Null(mail.LastAttemptUtc);
    }

    private async Task<MailOutboxEntry> Inspect(string recipient)
    {
        using var response = await api.Send("GET", "/api/admin/mail-outbox", "administrator");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token=", json);
        Assert.DoesNotContain("\"body\"", json);
        return Assert.Single((await response.Content.ReadFromJsonAsync<MailOutboxEntry[]>())!,
            item => item.Recipient == recipient);
    }

    private static AppDbContext Open(string path) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);

    private static OutboxSender Sender(AppDbContext db, int port) => new(db, new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SMTP_HOST"] = "127.0.0.1", ["SMTP_PORT"] = port.ToString(), ["SMTP_FROM"] = "queue@example.test",
            ["SMTP_USE_TLS"] = "false", ["SMTP_USE_AUTHENTICATION"] = "false", ["DEMO_MODE"] = "true"
        }).Build(), TimeProvider.System);

    private static async Task<string> AcceptMail(TcpListener listener, CancellationToken cancellationToken)
    {
        using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = connection.GetStream();
        using var reader = new StreamReader(stream);
        await using var writer = new StreamWriter(stream) { AutoFlush = true, NewLine = "\r\n" };
        await writer.WriteLineAsync("220 localhost SMTP");
        var lines = new List<string>();
        var recording = false;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line == ".")
            {
                recording = false;
                await writer.WriteLineAsync("250 Accepted");
            }
            else if (recording)
                lines.Add(line);
            else if (line == "DATA")
            {
                recording = true;
                await writer.WriteLineAsync("354 Start mail input");
            }
            else if (line == "QUIT")
            {
                await writer.WriteLineAsync("221 Closing");
                break;
            }
            else
                await writer.WriteLineAsync("250 OK");
        }
        return string.Join('\n', lines);
    }
}
