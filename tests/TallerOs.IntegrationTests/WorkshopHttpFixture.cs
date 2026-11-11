using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Access;
using TallerOs.Api.Data;
using TallerOs.Api.Infrastructure.Access;

namespace TallerOs.IntegrationTests;

public class WorkshopHttpFixture : IAsyncLifetime
{
    private readonly bool demoMode;
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("talleros-http-");
    private readonly Dictionary<string, string> tokens = [];
    private Process? server;
    private bool started;
    private HttpClient? client;

    public string DocumentsDatabasePath => Path.Combine(directory.FullName, "documents.db");
    public string DocumentsDirectory => Path.Combine(directory.FullName, "documents");

    public WorkshopHttpFixture() { }

    protected WorkshopHttpFixture(bool demoMode) => this.demoMode = demoMode;

    public async Task InitializeAsync()
    {
        try
        {
            var path = Path.Combine(directory.FullName, "http.db");
            var password = AccessService.NewSecret() + "a1";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={path};Pooling=False").Options;
            using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync();
                var hasher = new PasswordHasher<UserAccount>();
                foreach (var role in new[] { "administrator", "standard", "reader", "disabled" })
                {
                    var user = new UserAccount
                    {
                        Name = "Usuario ficticio", Email = $"{role}@example.test", EmailConfirmed = true,
                        Role = role == "administrator" ? UserRole.Administrator : UserRole.Standard,
                        IsEnabled = role != "disabled"
                    };
                    user.PasswordHash = hasher.HashPassword(user, password);
                    db.Users.Add(user);
                    if (role == "reader")
                        db.UserPermissions.Add(new UserPermission { UserId = user.Id, Permission = "WorkOrders.Read" });
                    if (role == "standard")
                    {
                        AddSession(db, user.Id, "expired", expired: true);
                        AddSession(db, user.Id, "revoked", revoked: true);
                    }
                    if (role == "disabled") AddSession(db, user.Id, "disabled");
                }
                await db.SaveChangesAsync();
            }

            var endpoint = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            var info = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory.FullName, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            info.ArgumentList.Add(typeof(AppDbContext).Assembly.Location);
            info.ArgumentList.Add("--urls");
            info.ArgumentList.Add("http://127.0.0.1:0");
            info.Environment["DB_PATH"] = path;
            info.Environment["DOCUMENT_DB_PATH"] = DocumentsDatabasePath;
            info.Environment["DOCUMENT_FILES_PATH"] = DocumentsDirectory;
            info.Environment["DEMO_MODE"] = demoMode ? "true" : "false";
            info.Environment["MAILPIT_URL"] = "http://127.0.0.1:1/";
            info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            info.Environment["DOTNET_ENVIRONMENT"] = "Production";
            info.Environment["Logging__LogLevel__Default"] = "Warning";
            info.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information";
            foreach (var key in info.Environment.Keys.Where(x => x.StartsWith("SMTP_", StringComparison.OrdinalIgnoreCase)).ToArray())
                info.Environment.Remove(key);
            server = new Process { StartInfo = info, EnableRaisingEvents = true };
            server.OutputDataReceived += (_, args) =>
            {
                const string marker = "Now listening on: ";
                var line = args.Data;
                if (line is null) return;
                var index = line.IndexOf(marker, StringComparison.Ordinal);
                if (index >= 0 && Uri.TryCreate(line[(index + marker.Length)..].Trim(), UriKind.Absolute, out var uri))
                    endpoint.TrySetResult(uri);
            };
            server.ErrorDataReceived += (_, _) => { };
            server.Exited += (_, _) => endpoint.TrySetException(new InvalidOperationException("La API de prueba terminó antes de estar disponible."));
            started = server.Start();
            if (!started) throw new InvalidOperationException("No se pudo iniciar la API de prueba.");
            server.BeginOutputReadLine();
            server.BeginErrorReadLine();
            client = new HttpClient { BaseAddress = await endpoint.Task.WaitAsync(TimeSpan.FromSeconds(30)) };
            client.Timeout = TimeSpan.FromSeconds(15);
            using var health = await client.GetAsync("/health");
            health.EnsureSuccessStatusCode();
            foreach (var role in new[] { "administrator", "standard", "reader" })
            {
                using var response = await client.PostAsJsonAsync("/api/access/login", new { email = $"{role}@example.test", password });
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                tokens[role] = body.GetProperty("token").GetString()
                    ?? throw new InvalidOperationException("El login de prueba no devolvió sesión.");
            }
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task<HttpResponseMessage> Send(string method, string path, string? role = null, string? json = null,
        HttpContent? content = null)
    {
        if (client is null) throw new InvalidOperationException("La API de prueba no está disponible.");
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (role is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
                role == "unknown" ? new string('A', 64) : tokens[role]);
        if (content is not null) request.Content = content;
        else if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (server is not null)
        {
            if (started && !server.HasExited)
            {
                server.Kill(entireProcessTree: true);
                await server.WaitForExitAsync();
            }
            server.Dispose();
            server = null;
        }
        if (directory.Exists)
        {
            foreach (var file in directory.EnumerateFiles()) file.Delete();
            directory.Delete(recursive: true);
        }
    }

    private void AddSession(AppDbContext db, Guid userId, string name, bool expired = false, bool revoked = false)
    {
        var secret = AccessService.NewSecret();
        tokens[name] = secret;
        var now = DateTimeOffset.UtcNow;
        db.Sessions.Add(new UserSession
        {
            UserId = userId, TokenHash = AccessService.HashSecret(secret), CreatedUtc = now.AddHours(-1),
            ExpiresUtc = expired ? now.AddMinutes(-1) : now.AddHours(1), RevokedUtc = revoked ? now : null
        });
    }
}
