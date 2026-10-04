using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;

namespace TallerOs.Api.Access;

public record RegisterRequest(string? Name, string? Email, string? Password);
public record EmailRequest(string? Email);
public record LoginRequest(string? Email, string? Password);
public record ResetRequest(string? Code, string? NewPassword);
public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public record ChangeRoleRequest(string? Role);

public static partial class AccessEndpoints
{
    public static void MapAccessEndpoints(this WebApplication app)
    {
        var access = app.MapGroup("/api/access");
        access.MapPost("/register", async (RegisterRequest request, AccessService service) =>
        {
            var (ok, message) = await service.Register(request.Name, request.Email, request.Password);
            return ok ? Results.Created("/api/access/login", new { message }) : Results.BadRequest(new { message });
        });
        access.MapGet("/activate", async (string? token, AccessService service) =>
            await service.Activate(token) ? Results.Text("Cuenta activada. Ya puedes iniciar sesión.")
                : Results.BadRequest(new { message = "Enlace no válido, usado o vencido." }));
        access.MapPost("/resend-activation", async (EmailRequest request, AccessService service) =>
        {
            await service.ResendActivation(request.Email);
            return Results.Ok(new { message = "Si la cuenta necesita activación, recibirás un correo." });
        });
    }
}
