using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;

namespace TallerOs.Api.Access;

public static partial class AccessEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin/users");
        admin.MapGet("/", async (AppDbContext db) =>
            Results.Ok(await db.Users.OrderBy(x => x.Email).Select(x => new
            {
                x.Id, x.Name, x.Email, role = x.Role.ToString(), active = x.IsEnabled, x.EmailConfirmed
            }).ToListAsync())).RequireRole(UserRole.Administrator);
        admin.MapPut("/{id:guid}/role", async (Guid id, ChangeRoleRequest request, AppDbContext db) =>
        {
            if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || !Enum.IsDefined(role))
                return Results.BadRequest(new { message = "Rol no válido." });
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound(new { message = "Usuario no encontrado." });
            user.Role = role;
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Rol actualizado." });
        }).RequireRole(UserRole.Administrator);
        admin.MapPut("/{id:guid}/enabled", async (Guid id, bool enabled, HttpContext http,
            AppDbContext db, AccessService service) =>
        {
            if (id == http.CurrentUser().Id && !enabled)
                return Results.BadRequest(new { message = "No puedes desactivarte a ti mismo." });
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound(new { message = "Usuario no encontrado." });
            user.IsEnabled = enabled;
            if (!enabled) await service.RevokeSessions(user.Id);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = enabled ? "Usuario reactivado." : "Usuario desactivado." });
        }).RequireRole(UserRole.Administrator);
        admin.MapPost("/{id:guid}/force-reset", async (Guid id, AppDbContext db, AccessService service) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound(new { message = "Usuario no encontrado." });
            await service.ForceReset(user);
            return Results.Ok(new { message = "Contraseña anterior invalidada. Código enviado a la cola." });
        }).RequireRole(UserRole.Administrator);
    }
}
