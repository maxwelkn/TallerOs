using TallerOs.Domain.Permissions;

namespace TallerOs.UnitTests;

public sealed class PermissionRequestTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RF_GP_01_Create_StartsPending()
    {
        var requester = Guid.NewGuid();

        var request = PermissionRequest.Create(requester, "Orders.Read", Created);

        Assert.Equal(PermissionRequestStatus.Pending, request.Status);
        Assert.Equal(requester, request.RequesterId);
        Assert.Equal(Created, request.CreatedUtc);
        Assert.Null(request.ResolverId);
    }

    [Fact]
    public void RF_GP_03_Approve_RecordsAdministratorAndResolutionTime()
    {
        var request = NewRequest();
        var administrator = Guid.NewGuid();
        var resolved = Created.AddMinutes(1);

        request.Approve(administrator, resolved);

        Assert.Equal(PermissionRequestStatus.Approved, request.Status);
        Assert.Equal(administrator, request.ResolverId);
        Assert.Equal(resolved, request.ResolvedUtc);
    }

    [Fact]
    public void RF_GP_04_Reject_RecordsReasonAndResolver()
    {
        var request = NewRequest();
        var administrator = Guid.NewGuid();
        var resolved = Created.AddMinutes(2);

        request.Reject(administrator, resolved, "  Acceso no requerido  ");

        Assert.Equal(PermissionRequestStatus.Rejected, request.Status);
        Assert.Equal("Acceso no requerido", request.RejectionReason);
        Assert.Equal(administrator, request.ResolverId);
        Assert.Equal(resolved, request.ResolvedUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RF_GP_04_RejectWithoutReason_PreservesPendingRequest(string? reason)
    {
        var request = NewRequest();

        Assert.Throws<PermissionRuleException>(() => request.Reject(Guid.NewGuid(), Created, reason));

        Assert.Equal(PermissionRequestStatus.Pending, request.Status);
        Assert.Null(request.ResolverId);
        Assert.Null(request.ResolvedUtc);
    }

    [Fact]
    public void RF_GP_06_ApproveTwice_RejectsAndPreservesResolution()
    {
        var request = NewRequest();
        var administrator = Guid.NewGuid();
        request.Approve(administrator, Created);

        Assert.Throws<PermissionRuleException>(() => request.Approve(Guid.NewGuid(), Created.AddMinutes(5)));

        Assert.Equal(PermissionRequestStatus.Approved, request.Status);
        Assert.Equal(administrator, request.ResolverId);
        Assert.Equal(Created, request.ResolvedUtc);
    }

    [Fact]
    public void RF_GP_06_ApplyRejectedRequest_RejectsAndPreservesReason()
    {
        var request = NewRequest();
        request.Reject(Guid.NewGuid(), Created, "Sin justificación");

        Assert.Throws<PermissionRuleException>(request.Apply);

        Assert.Equal(PermissionRequestStatus.Rejected, request.Status);
        Assert.Equal("Sin justificación", request.RejectionReason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RF_GP_07_ResolveOwnRequest_RejectsAndPreservesPending(bool approve)
    {
        var request = NewRequest();
        Action resolve = approve
            ? () => request.Approve(request.RequesterId, Created)
            : () => request.Reject(request.RequesterId, Created, "Motivo");

        Assert.Throws<PermissionRuleException>(resolve);

        Assert.Equal(PermissionRequestStatus.Pending, request.Status);
        Assert.Null(request.ResolverId);
    }

    [Theory]
    [InlineData(PermissionRequestStatus.Pending, "apply")]
    [InlineData(PermissionRequestStatus.Approved, "approve")]
    [InlineData(PermissionRequestStatus.Approved, "reject")]
    [InlineData(PermissionRequestStatus.Applied, "approve")]
    [InlineData(PermissionRequestStatus.Applied, "reject")]
    [InlineData(PermissionRequestStatus.Applied, "apply")]
    [InlineData(PermissionRequestStatus.Rejected, "approve")]
    [InlineData(PermissionRequestStatus.Rejected, "reject")]
    [InlineData(PermissionRequestStatus.Rejected, "apply")]
    public void RF_GP_06_ForbiddenOperation_PreservesRequest(PermissionRequestStatus initial, string operation)
    {
        var request = NewRequest();
        var administrator = Guid.NewGuid();
        if (initial == PermissionRequestStatus.Rejected) request.Reject(administrator, Created, "Motivo");
        if (initial is PermissionRequestStatus.Approved or PermissionRequestStatus.Applied)
            request.Approve(administrator, Created);
        if (initial == PermissionRequestStatus.Applied) request.Apply();
        var resolver = request.ResolverId;
        var resolved = request.ResolvedUtc;
        var reason = request.RejectionReason;
        Action act = operation switch
        {
            "approve" => () => request.Approve(Guid.NewGuid(), Created.AddHours(1)),
            "reject" => () => request.Reject(Guid.NewGuid(), Created.AddHours(1), "Otro motivo"),
            _ => request.Apply
        };

        Assert.Throws<PermissionRuleException>(act);

        Assert.Equal(initial, request.Status);
        Assert.Equal(resolver, request.ResolverId);
        Assert.Equal(resolved, request.ResolvedUtc);
        Assert.Equal(reason, request.RejectionReason);
    }

    private static PermissionRequest NewRequest() => PermissionRequest.Create(Guid.NewGuid(), "Orders.Read", Created);
}
