using TallerOs.Api.Http;
using TallerOs.Application.Common;
using TallerOs.Application.Permissions;

namespace TallerOs.Api.Permissions;

public sealed record CreatePermissionRequest(string? Permission);
public sealed record RejectPermissionRequest(string? Reason);

public static class PermissionEndpoints
{
    public static void MapPermissionEndpoints(this WebApplication app)
    {
        var permissions = app.MapGroup("/api/permissions/requests");
        permissions.MapPost("", async (CreatePermissionRequest request, PermissionRequestService service,
            CancellationToken cancellationToken) =>
        {
            var outcome = await service.Create(request.Permission, cancellationToken);
            return outcome.Failure == Failure.None && outcome.Value is { } value
                ? Results.Created("/api/permissions/requests/mine", value) : outcome.ToHttp();
        });
        permissions.MapGet("/mine", async (PermissionRequestService service, CancellationToken cancellationToken) =>
            (await service.List(false, cancellationToken)).ToHttp());
        permissions.MapGet("/pending", async (PermissionRequestService service, CancellationToken cancellationToken) =>
            (await service.List(true, cancellationToken)).ToHttp());
        permissions.MapPost("/{id:guid}/approve", async (Guid id, PermissionRequestService service,
            CancellationToken cancellationToken) =>
            (await service.Resolve(id, true, cancellationToken: cancellationToken)).ToHttp());
        permissions.MapPost("/{id:guid}/reject", async (Guid id, RejectPermissionRequest request,
            PermissionRequestService service, CancellationToken cancellationToken) =>
            (await service.Resolve(id, false, request.Reason, cancellationToken)).ToHttp());
    }
}
