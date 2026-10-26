namespace TallerOs.Domain.Permissions;

public enum PermissionRequestStatus { Pending, Approved, Applied, Rejected }

public sealed class PermissionRuleException(string message) : Exception(message);

public sealed class PermissionRequest
{
    public Guid Id { get; private set; }
    public Guid RequesterId { get; private set; }
    public string Permission { get; private set; } = "";
    public PermissionRequestStatus Status { get; private set; }
    public DateTimeOffset CreatedUtc { get; private set; }
    public Guid? ResolverId { get; private set; }
    public DateTimeOffset? ResolvedUtc { get; private set; }
    public string? RejectionReason { get; private set; }

    private PermissionRequest() { }

    public static PermissionRequest Create(Guid requesterId, string permission, DateTimeOffset createdUtc)
    {
        if (requesterId == Guid.Empty || string.IsNullOrWhiteSpace(permission) || permission.Length > 100)
            throw new PermissionRuleException("Solicitante o permiso no válido.");

        return new PermissionRequest
        {
            Id = Guid.NewGuid(), RequesterId = requesterId, Permission = permission.Trim(),
            Status = PermissionRequestStatus.Pending, CreatedUtc = createdUtc.ToUniversalTime()
        };
    }

    public void Approve(Guid resolverId, DateTimeOffset resolvedUtc)
    {
        EnsureResolver(resolverId, resolvedUtc);
        MoveTo(PermissionRequestStatus.Approved);
        ResolverId = resolverId;
        ResolvedUtc = resolvedUtc.ToUniversalTime();
    }

    public void Reject(Guid resolverId, DateTimeOffset resolvedUtc, string? reason)
    {
        EnsureResolver(resolverId, resolvedUtc);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            throw new PermissionRuleException("El motivo es obligatorio y admite hasta 1000 caracteres.");

        MoveTo(PermissionRequestStatus.Rejected);
        ResolverId = resolverId;
        ResolvedUtc = resolvedUtc.ToUniversalTime();
        RejectionReason = reason.Trim();
    }

    public void Apply() => MoveTo(PermissionRequestStatus.Applied);

    private void EnsureResolver(Guid resolverId, DateTimeOffset resolvedUtc)
    {
        if (resolverId == Guid.Empty || resolverId != RequesterId)
            throw new PermissionRuleException("No puedes resolver tu propia solicitud ni usar un responsable vacío.");
        if (resolvedUtc < CreatedUtc)
            throw new PermissionRuleException("La resolución no puede ser anterior a la creación.");
    }

    private void MoveTo(PermissionRequestStatus next)
    {
        if (!PermissionRequestTransitions.IsAllowed(Status, next))
            throw new PermissionRuleException($"Transición de {Status} a {next} no permitida.");
        Status = next;
    }
}

public static class PermissionRequestTransitions
{
    public static bool IsAllowed(PermissionRequestStatus from, PermissionRequestStatus to) => (from, to) is
        (PermissionRequestStatus.Pending, PermissionRequestStatus.Approved) or
        (PermissionRequestStatus.Approved, PermissionRequestStatus.Applied) or
        (PermissionRequestStatus.Pending, PermissionRequestStatus.Rejected);
}
