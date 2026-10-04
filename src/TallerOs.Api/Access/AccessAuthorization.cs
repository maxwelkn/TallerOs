using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;

namespace TallerOs.Api.Access;

public static class AccessAuthorization
{
    // Null means the operation is public. Every access route is declared here.
    private static readonly IReadOnlyDictionary<(string Method, string Route), UserRole?> RequiredRoles =
        new Dictionary<(string, string), UserRole?>
        {
            [("POST", "/api/access/register")] = null,
            [("GET", "/api/access/activate")] = null,
            [("POST", "/api/access/resend-activation")] = null,
            [("POST", "/api/access/login")] = null,
            [("GET", "/api/access/me")] = UserRole.Standard,
            [("POST", "/api/access/logout")] = UserRole.Standard,
            [("POST", "/api/access/forgot-password")] = null,
            [("POST", "/api/access/reset-password")] = null,
            [("POST", "/api/access/change-password")] = UserRole.Standard,
            [("GET", "/api/admin/users")] = UserRole.Administrator,
            [("PUT", "/api/admin/users/{id:guid}/role")] = UserRole.Administrator,
            [("PUT", "/api/admin/users/{id:guid}/enabled")] = UserRole.Administrator,
            [("POST", "/api/admin/users/{id:guid}/force-reset")] = UserRole.Administrator
        };

    public static WebApplication UseAccessAuthorization(this WebApplication app)
    {
        app.Use(async (http, next) =>
        {
            var endpoint = http.GetEndpoint() as RouteEndpoint;
            if (endpoint is null || (!http.Request.Path.StartsWithSegments("/api/access")
                && !http.Request.Path.StartsWithSegments("/api/admin")))
            {
                await next(http);
                return;
            }

            var route = "/" + endpoint.RoutePattern.RawText?.Trim('/');
            if (!RequiredRoles.TryGetValue((http.Request.Method, route), out var required))
            {
                await Results.StatusCode(StatusCodes.Status403Forbidden).ExecuteAsync(http);
                return;
            }

            if (required is null)
            {
                await next(http);
                return;
            }

            var header = http.Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                await Results.Unauthorized().ExecuteAsync(http);
                return;
            }

            var secret = header[7..].Trim();
            if (secret.Length != 64)
            {
                await Results.Unauthorized().ExecuteAsync(http);
                return;
            }

            var db = http.RequestServices.GetRequiredService<AppDbContext>();
            var now = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var hash = AccessService.HashSecret(secret);
            var session = await db.Sessions.FirstOrDefaultAsync(x => x.TokenHash == hash);
            if (session is null || session.RevokedUtc is not null || session.ExpiresUtc <= now)
            {
                await Results.Unauthorized().ExecuteAsync(http);
                return;
            }

            var user = await db.Users.FindAsync(session.UserId);
            if (user is null || !user.EmailConfirmed || !user.IsEnabled)
            {
                await Results.Unauthorized().ExecuteAsync(http);
                return;
            }

            if (required == UserRole.Administrator && user.Role != UserRole.Administrator)
            {
                await Results.StatusCode(StatusCodes.Status403Forbidden).ExecuteAsync(http);
                return;
            }

            http.Items["user"] = user;
            http.Items["session"] = session;
            await next(http);
        });
        return app;
    }

    public static UserAccount CurrentUser(this HttpContext http) => (UserAccount)http.Items["user"]!;
    public static UserSession CurrentSession(this HttpContext http) => (UserSession)http.Items["session"]!;
}
