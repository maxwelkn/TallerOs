using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Access;
using TallerOs.Api.Data;
using TallerOs.Api.Mail;

var builder = WebApplication.CreateBuilder(args);
var dbPath = builder.Configuration["DB_PATH"] ?? Path.Combine("data", "talleros.db");
var absoluteDbPath = Path.GetFullPath(dbPath);
Directory.CreateDirectory(Path.GetDirectoryName(absoluteDbPath)!);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={absoluteDbPath}"));
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<OutboxSender>();
builder.Services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

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
await app.RunAsync();
