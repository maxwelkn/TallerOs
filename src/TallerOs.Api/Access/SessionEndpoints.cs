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
        });
        access.MapPost("/logout", async (HttpContext http, AccessService service) =>
        {
            await service.Logout(http.CurrentSession());
            return Results.Ok(new { message = "Sesión cerrada." });
        });
    }
}
