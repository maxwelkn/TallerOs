namespace TallerOs.Api.Access;

public static partial class AccessEndpoints
{
    public static void MapPasswordEndpoints(this WebApplication app)
    {
        var access = app.MapGroup("/api/access");
        access.MapPost("/forgot-password", async (EmailRequest request, AccessService service) =>
        {
            await service.RequestRecovery(request.Email);
            return Results.Ok(new { message = "Si el correo está registrado, recibirás instrucciones." });
        });
        access.MapPost("/reset-password", async (ResetRequest request, AccessService service) =>
        {
            var (ok, message) = await service.ResetPassword(request.Code, request.NewPassword);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        });
        access.MapPost("/change-password", async (ChangePasswordRequest request, HttpContext http, AccessService service) =>
        {
            var (ok, message) = await service.ChangePassword(http.CurrentUser(), request.CurrentPassword, request.NewPassword);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        });

    }
}
