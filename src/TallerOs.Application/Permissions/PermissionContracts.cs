using TallerOs.Domain.Permissions;

namespace TallerOs.Application.Permissions;

public sealed record PermissionRequestView(Guid Id, Guid RequesterId, string Permission, string Status,
    DateTimeOffset CreatedUtc, Guid? ResolverId, DateTimeOffset? ResolvedUtc, string? RejectionReason)
{
    public static PermissionRequestView From(PermissionRequest request) => new(request.Id,
        request.RequesterId, request.Permission, request.Status.ToString(), request.CreatedUtc,
        request.ResolverId, request.ResolvedUtc, request.RejectionReason);
}

public interface IPermissionRequestStore
{
    Task<PermissionRequest?> Find(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PermissionRequest>> List(Guid? requesterId, bool pendingOnly,
        CancellationToken cancellationToken);
    void Add(PermissionRequest request);
    Task<bool> Save(CancellationToken cancellationToken);
}
