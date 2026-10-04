using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;

namespace TallerOs.Api.Access;

public static partial class AccessEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        var access = app.MapGroup("/api/access");
        access.MapPost("/login", async (LoginRequest request, AccessService service) =>
        {
            var (token, message) = await service.Login(request.Email, request.Password);
            return token is null ? Results.BadRequest(new { message }) : Results.Ok(new { token, message });
        });
        access.MapGet("/me", (HttpContext http) =>
        {
            var user = http.CurrentUser();
            return Results.Ok(new { user.Id, user.Name, user.Email, role = user.Role.ToString(), active = user.IsEnabled });
        }).RequireRole(UserRole.Standard);
        access.MapPost("/logout", async (HttpContext http, AccessService service) =>
        {
            await service.Logout(http.CurrentSession());
            return Results.Ok(new { message = "Sesión cerrada." });
        }).RequireRole(UserRole.Standard);
    }
}

public static class RoleGuard
{
    public static RouteHandlerBuilder RequireRole(this RouteHandlerBuilder builder, UserRole required) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var value = http.Request.Headers.Authorization.ToString();
            if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Results.Unauthorized();
            var secret = value[7..].Trim();
            if (secret.Length != 64) return Results.Unauthorized();
            var db = http.RequestServices.GetRequiredService<AppDbContext>();
            var now = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var hash = AccessService.HashSecret(secret);
            var session = await db.Sessions.FirstOrDefaultAsync(x => x.TokenHash == hash);
            if (session is null || session.RevokedUtc is not null || session.ExpiresUtc <= now)
                return Results.Unauthorized();
            var user = await db.Users.FindAsync(session.UserId);
            if (user is null || !user.EmailConfirmed || !user.IsEnabled) return Results.Unauthorized();
            if (required == UserRole.Administrator && user.Role != UserRole.Administrator)
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            http.Items["user"] = user;
            http.Items["session"] = session;
            return await next(context);
        });

    public static UserAccount CurrentUser(this HttpContext http) => (UserAccount)http.Items["user"]!;
    public static UserSession CurrentSession(this HttpContext http) => (UserSession)http.Items["session"]!;
}
