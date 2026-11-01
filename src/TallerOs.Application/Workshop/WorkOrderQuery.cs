using TallerOs.Application.Access;
using TallerOs.Application.Common;

namespace TallerOs.Application.Workshop;

public sealed record WorkOrderView(Guid Id, Guid VehicleId, string Reason, string Status);

public interface IWorkOrderReader
{
    Task<IReadOnlyList<WorkOrderView>> List(CancellationToken cancellationToken);
    Task<WorkOrderDetailsView?> Find(Guid id, CancellationToken cancellationToken);
}

public sealed class WorkOrderQuery(ICurrentActor currentActor, IAccessPermissions access, IWorkOrderReader reader)
{
    public const string ReadPermission = "WorkOrders.Read";

    public async Task<Outcome<IReadOnlyList<WorkOrderView>>> List(CancellationToken cancellationToken = default)
    {
        var authorization = await CheckAccess(cancellationToken);
        if (authorization.Failure != Failure.None)
            return Outcome<IReadOnlyList<WorkOrderView>>.Fail(authorization.Failure, authorization.Message);
        return new(await reader.List(cancellationToken));
    }

    public async Task<Outcome<WorkOrderDetailsView>> Find(Guid id, CancellationToken cancellationToken = default)
    {
        var authorization = await CheckAccess(cancellationToken);
        if (authorization.Failure != Failure.None)
            return Outcome<WorkOrderDetailsView>.Fail(authorization.Failure, authorization.Message);
        if (id == Guid.Empty)
            return Outcome<WorkOrderDetailsView>.Fail(Failure.Invalid, "El identificador de la orden no es válido.");
        var order = await reader.Find(id, cancellationToken);
        return order is null
            ? Outcome<WorkOrderDetailsView>.Fail(Failure.NotFound, "Orden no encontrada.") : new(order);
    }

    private async Task<(Failure Failure, string Message)> CheckAccess(CancellationToken cancellationToken)
    {
        var actor = currentActor.Get();
        if (actor is null || actor.Id == Guid.Empty)
            return (Failure.Unauthorized, "Inicia sesión.");
        if (!actor.IsAdministrator && !await access.HasPermission(actor.Id, ReadPermission, cancellationToken))
            return (Failure.Forbidden, "Se requiere el permiso WorkOrders.Read.");
        return (Failure.None, "");
    }
}
