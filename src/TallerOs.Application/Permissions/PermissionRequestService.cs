using TallerOs.Application.Common;
using TallerOs.Application.Access;
using TallerOs.Domain.Permissions;

namespace TallerOs.Application.Permissions;

public sealed class PermissionRequestService(IPermissionRequestStore store, ICurrentActor currentActor,
    IAccessPermissions access, IPermissionCatalog catalog, TimeProvider clock)
{
    public async Task<Outcome<PermissionRequestView>> Create(string? permission, CancellationToken cancellationToken = default)
    {
        var actor = currentActor.Get();
        if (actor is null) return Outcome<PermissionRequestView>.Fail(Failure.Unauthorized, "Inicia sesión.");
        if (actor.IsAdministrator)
            return Outcome<PermissionRequestView>.Fail(Failure.Forbidden, "Solo un usuario Estándar solicita permisos.");
        if (string.IsNullOrWhiteSpace(permission) || !catalog.Contains(permission))
            return Outcome<PermissionRequestView>.Fail(Failure.Invalid, "Permiso no válido.");
        if (await access.HasPermission(actor.Id, permission, cancellationToken))
            return Outcome<PermissionRequestView>.Fail(Failure.Conflict, "Ya tienes ese permiso.");

        var request = PermissionRequest.Create(actor.Id, permission, clock.GetUtcNow());
        store.Add(request);
        if (!await store.Save(cancellationToken)) return Conflict();
        return new(PermissionRequestView.From(request));
    }

    public async Task<Outcome<IReadOnlyList<PermissionRequestView>>> List(bool pendingOnly,
        CancellationToken cancellationToken = default)
    {
        var actor = currentActor.Get();
        if (actor is null)
            return Outcome<IReadOnlyList<PermissionRequestView>>.Fail(Failure.Unauthorized, "Inicia sesión.");
        if (pendingOnly && !actor.IsAdministrator)
            return Outcome<IReadOnlyList<PermissionRequestView>>.Fail(Failure.Forbidden, "Se requiere Administrador.");
        var requests = await store.List(pendingOnly ? null : actor.Id, pendingOnly, cancellationToken);
        return new(requests.Select(PermissionRequestView.From).ToArray());
    }

    public async Task<Outcome<PermissionRequestView>> Resolve(Guid id, bool approve, string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var actor = currentActor.Get();
        if (actor is null) return Outcome<PermissionRequestView>.Fail(Failure.Unauthorized, "Inicia sesión.");
        if (!actor.IsAdministrator)
            return Outcome<PermissionRequestView>.Fail(Failure.Forbidden, "Se requiere Administrador.");
        var request = await store.Find(id, cancellationToken);
        if (request is null) return Outcome<PermissionRequestView>.Fail(Failure.NotFound, "Solicitud no encontrada.");
        if (request.RequesterId == actor.Id)
            return Outcome<PermissionRequestView>.Fail(Failure.Forbidden, "No puedes resolver tu propia solicitud.");
        if (!approve && (reason is null || reason.Length > 1000))
            return Outcome<PermissionRequestView>.Fail(Failure.Invalid, "Indica un motivo de hasta 1000 caracteres.");

        try
        {
            if (approve)
            {
                request.Approve(actor.Id, clock.GetUtcNow());
                await access.StageGrant(request.RequesterId, request.Permission, cancellationToken);
                request.Apply();
            }
            else request.Reject(actor.Id, clock.GetUtcNow(), reason);
        }
        catch (PermissionRuleException error)
        {
            return Outcome<PermissionRequestView>.Fail(Failure.Conflict, error.Message);
        }

        if (!await store.Save(cancellationToken)) return Conflict();
        return new(PermissionRequestView.From(request));
    }

    private static Outcome<PermissionRequestView> Conflict() =>
        Outcome<PermissionRequestView>.Fail(Failure.Conflict, "La solicitud cambió. Consulta su estado antes de reintentar.");
}
