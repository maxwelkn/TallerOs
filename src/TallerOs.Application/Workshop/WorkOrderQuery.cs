using TallerOs.Application.Access;
using TallerOs.Application.Common;

namespace TallerOs.Application.Workshop;

public sealed record WorkOrderView(Guid Id, Guid VehicleId, string Reason, string Status);

public interface IWorkOrderReader
{
    Task<IReadOnlyList<WorkOrderView>> List(CancellationToken cancellationToken);
}

public sealed class WorkOrderQuery(ICurrentActor currentActor, IAccessPermissions access, IWorkOrderReader reader)
{
    public const string ReadPermission = "WorkOrders.Read";

    public async Task<Outcome<IReadOnlyList<WorkOrderView>>> List(CancellationToken cancellationToken = default)
    {
        var actor = currentActor.Get();
        if (actor is null)
            return Outcome<IReadOnlyList<WorkOrderView>>.Fail(Failure.Unauthorized, "Inicia sesión.");
        if (!actor.IsAdministrator && !await access.HasPermission(actor.Id, ReadPermission, cancellationToken))
            return Outcome<IReadOnlyList<WorkOrderView>>.Fail(Failure.Forbidden, "Se requiere el permiso WorkOrders.Read.");
        return new(await reader.List(cancellationToken));
    }
}
