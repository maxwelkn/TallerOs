using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Access;
using TallerOs.Api.Data;
using TallerOs.Api.Mail;
using TallerOs.Api.Infrastructure.Access;
using TallerOs.Api.Infrastructure.Permissions;
using TallerOs.Api.Infrastructure.Workshop;
using TallerOs.Api.Permissions;
using TallerOs.Api.Workshop;
using TallerOs.Application.Access;
using TallerOs.Application.Permissions;
using TallerOs.Application.Workshop;
using TallerOs.Api.Documents;
using TallerOs.Application.Documents;
using TallerOs.Infrastructure.Documents;
using TallerOs.Application.Mail;
using TallerOs.Infrastructure.Mail;
using TallerOs.Infrastructure.Workshop;

if (args is ["check-health", var healthAddress])
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    try
    {
        using var response = await client.GetAsync(healthAddress);
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
    {
        Environment.ExitCode = 1;
    }
    return;
}

var builder = WebApplication.CreateBuilder(args);
var demoMode = builder.Configuration.GetValue("DEMO_MODE", false);
builder.Services.AddSingleton(new DemoMailboxSettings(demoMode));
builder.Services.AddHttpClient<IDemoMailbox, MailpitMailbox>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MAILPIT_URL"] ?? "http://mailpit:8025/");
    client.Timeout = TimeSpan.FromSeconds(5);
    client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
});
builder.Services.AddScoped<DemoMailboxService>();
builder.Services.AddSingleton(new WeatherApiSettings(Environment.GetEnvironmentVariable("WEATHER_API_KEY"),
    builder.Configuration["WORKSHOP_LOCATION"] ?? "Santo Domingo"));
builder.Services.AddHttpClient<IWorkshopWeather, WeatherApiWeather>(client =>
{
    client.BaseAddress = new Uri("https://api.weatherapi.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.MaxResponseContentBufferSize = 65536;
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
    .RemoveAllLoggers();
builder.Services.AddScoped<DeliveryReviewService>();
var workingDirectory = Directory.GetCurrentDirectory();
var solutionRoot = new DirectoryInfo(workingDirectory);
while (solutionRoot.Parent is not null && !File.Exists(Path.Combine(solutionRoot.FullName, "TallerOs.slnx")))
    solutionRoot = solutionRoot.Parent;
var baseDirectory = File.Exists(Path.Combine(solutionRoot.FullName, "TallerOs.slnx"))
    ? solutionRoot.FullName : workingDirectory;
var dbPath = builder.Configuration["DB_PATH"] ?? Path.Combine("data", "talleros.db");
var absoluteDbPath = Path.GetFullPath(dbPath, baseDirectory);
Directory.CreateDirectory(Path.GetDirectoryName(absoluteDbPath)!);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={absoluteDbPath}"));
var dataDirectory = Path.GetDirectoryName(absoluteDbPath) ?? baseDirectory;
var documentDbPath = Path.GetFullPath(builder.Configuration["DOCUMENT_DB_PATH"]
    ?? Path.Combine(dataDirectory, "documents.db"), baseDirectory);
var documentFilesPath = Path.GetFullPath(builder.Configuration["DOCUMENT_FILES_PATH"]
    ?? Path.Combine(dataDirectory, "documents"), baseDirectory);
if (string.Equals(documentDbPath, absoluteDbPath, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("DOCUMENT_DB_PATH debe ser distinta de DB_PATH.");
Directory.CreateDirectory(Path.GetDirectoryName(documentDbPath) ?? baseDirectory);
builder.Services.AddDbContext<DocumentDbContext>(options => options.UseSqlite($"Data Source={documentDbPath}"));
builder.Services.AddScoped<IDocumentStore, DocumentStore>();
builder.Services.AddSingleton<IDocumentFiles>(services =>
    new LocalDocumentFiles(documentFilesPath, services.GetRequiredService<ILogger<LocalDocumentFiles>>()));
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<OutboxSender>();
builder.Services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();
builder.Services.AddScoped<IAccessPermissions, AccessPermissions>();
builder.Services.AddSingleton<IPermissionCatalog>(new ConfiguredPermissionCatalog([WorkOrderQuery.ReadPermission]));
builder.Services.AddScoped<IPermissionRequestStore, PermissionRequestStore>();
builder.Services.AddScoped<PermissionRequestService>();
builder.Services.AddScoped<IWorkOrderReader, WorkOrderReader>();
builder.Services.AddScoped<WorkOrderQuery>();
builder.Services.AddScoped<ICustomerStore, CustomerStore>();
builder.Services.AddScoped<IVehicleStore, VehicleStore>();
builder.Services.AddScoped<IWorkOrderStore, WorkOrderStore>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<WorkOrderService>();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseRouting();
app.UseAccessAuthorization();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DocumentDbContext>().Database.MigrateAsync();

    if (args.Contains("bootstrap-admin"))
    {
        if (await db.Users.AnyAsync(x => x.Role == UserRole.Administrator))
        {
            Console.WriteLine("Ya existe un Administrador.");
            return;
        }
        var email = builder.Configuration["ADMIN_EMAIL"]?.Trim().ToLowerInvariant();
        var password = builder.Configuration["ADMIN_PASSWORD"];
        if (!AccessService.ValidEmail(email) || !AccessService.ValidPassword(password))
            throw new InvalidOperationException("Configura ADMIN_EMAIL y ADMIN_PASSWORD válidos.");
        var user = new UserAccount { Name = "Administrador", Email = email!, Role = UserRole.Administrator,
            EmailConfirmed = true };
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserAccount>>();
        user.PasswordHash = hasher.HashPassword(user, password!);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        Console.WriteLine("Administrador inicial creado.");
        return;
    }

    if (args.Contains("send-mail"))
    {
        var sender = scope.ServiceProvider.GetRequiredService<OutboxSender>();
        Console.WriteLine($"Correos enviados: {await sender.SendPending()}");
        return;
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAccessEndpoints();
app.MapSessionEndpoints();
app.MapPasswordEndpoints();
app.MapAdminEndpoints();
app.MapPermissionEndpoints();
app.MapCustomerEndpoints();
app.MapVehicleEndpoints();
app.MapWorkOrderEndpoints();
app.MapDocumentEndpoints();
if (demoMode) app.MapDemoMailboxEndpoints();
await app.RunAsync();
