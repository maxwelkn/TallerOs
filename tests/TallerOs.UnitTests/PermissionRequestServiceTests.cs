using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Application.Permissions;
using TallerOs.Application.Workshop;
using TallerOs.Domain.Permissions;

namespace TallerOs.UnitTests;

public sealed class PermissionRequestServiceTests
{
    [Fact]
    public async Task RF_GP_01_Create_UsesAuthenticatedRequester()
    {
        var scenario = new Scenario(false);

        var result = await scenario.Service.Create(WorkOrderQuery.ReadPermission);

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(scenario.Actor.Get()!.Id, result.Value!.RequesterId);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Single(scenario.Store.Requests);
    }

    [Fact]
    public async Task RF_GP_02_ListMine_ExcludesAnotherUsersRequests()
    {
        var scenario = new Scenario(false);
        var own = scenario.AddRequest(scenario.Actor.Get()!.Id);
        scenario.AddRequest(Guid.NewGuid());

        var result = await scenario.Service.List(false);

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(own.Id, Assert.Single(result.Value!).Id);
    }

    [Fact]
    public async Task RF_GP_02_StandardCannotListPendingRequests()
    {
        var scenario = new Scenario(false);
        scenario.AddRequest(Guid.NewGuid());

        var result = await scenario.Service.List(true);

        Assert.Equal(Failure.Forbidden, result.Failure);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RF_GP_03_RF_GP_04_StandardCannotResolveRequests(bool approve)
    {
        var scenario = new Scenario(false);
        var request = scenario.AddRequest(Guid.NewGuid());

        var result = await scenario.Service.Resolve(request.Id, approve, "Motivo");

        Assert.Equal(Failure.Forbidden, result.Failure);
        Assert.Null(result.Value);
        Assert.Equal(PermissionRequestStatus.Pending, request.Status);
        Assert.Null(request.ResolverId);
        Assert.Null(request.ResolvedUtc);
        Assert.Equal(0, scenario.Store.SaveCount);
        Assert.False(await scenario.Access.HasPermission(request.RequesterId, request.Permission, default));
    }

    [Fact]
    public async Task RF_GP_03_Approve_ReturnsAdministratorAndResolutionTime()
    {
        var scenario = new Scenario(true);
        var administrator = scenario.Actor.Get()!.Id;
        var request = scenario.AddRequest(Guid.NewGuid());

        var result = await scenario.Service.Resolve(request.Id, true);

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(administrator, result.Value!.ResolverId);
        Assert.Equal(scenario.Clock.GetUtcNow(), result.Value.ResolvedUtc);
        Assert.Equal(administrator, request.ResolverId);
        Assert.Equal(scenario.Clock.GetUtcNow(), request.ResolvedUtc);
    }

    [Fact]
    public async Task RF_GP_05_Approve_AppliesPermissionAndUnlocksAction()
    {
        var scenario = new Scenario(true);
        var administrator = scenario.Actor.Get()!.Id;
        var requester = Guid.NewGuid();
        var request = scenario.AddRequest(requester);
        var reader = new FakeOrderReader();

        var result = await scenario.Service.Resolve(request.Id, true);
        scenario.Actor.Value = new Actor(requester, false);
        var orders = await new WorkOrderQuery(scenario.Actor, scenario.Access, reader).List();

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal("Applied", result.Value!.Status);
        Assert.Equal(administrator, result.Value.ResolverId);
        Assert.Equal(scenario.Clock.GetUtcNow(), result.Value.ResolvedUtc);
        Assert.Equal(Failure.None, orders.Failure);
        Assert.True(reader.WasRead);
        Assert.Equal(1, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RF_GP_05_MissingPermission_DeniesActionWithoutReadingOrders()
    {
        var scenario = new Scenario(false);
        var reader = new FakeOrderReader();

        var result = await new WorkOrderQuery(scenario.Actor, scenario.Access, reader).List();

        Assert.Equal(Failure.Forbidden, result.Failure);
        Assert.False(reader.WasRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RF_GP_07_PromotedRequesterCannotResolveOwnRequest(bool approve)
    {
        var scenario = new Scenario(true);
        var request = scenario.AddRequest(scenario.Actor.Get()!.Id);

        var result = await scenario.Service.Resolve(request.Id, approve, "Motivo");

        Assert.Equal(Failure.Forbidden, result.Failure);
        Assert.Null(result.Value);
        Assert.Equal(PermissionRequestStatus.Pending, request.Status);
        Assert.Null(request.ResolverId);
        Assert.Null(request.ResolvedUtc);
        Assert.Equal(0, scenario.Store.SaveCount);
        Assert.False(await scenario.Access.HasPermission(request.RequesterId, request.Permission, default));
    }

    [Theory]
    [InlineData(PermissionRequestStatus.Approved, false)]
    [InlineData(PermissionRequestStatus.Approved, true)]
    [InlineData(PermissionRequestStatus.Applied, false)]
    [InlineData(PermissionRequestStatus.Applied, true)]
    [InlineData(PermissionRequestStatus.Rejected, false)]
    [InlineData(PermissionRequestStatus.Rejected, true)]
    public async Task RF_GP_06_ResolveNonPendingRequest_PreservesResolution(
        PermissionRequestStatus initial, bool approve)
    {
        var scenario = new Scenario(true);
        var request = scenario.AddRequest(Guid.NewGuid());
        var originalResolver = Guid.NewGuid();
        var originalTime = scenario.Clock.GetUtcNow().AddMinutes(1);
        if (initial == PermissionRequestStatus.Rejected)
            request.Reject(originalResolver, originalTime, "Motivo original");
        else
        {
            request.Approve(originalResolver, originalTime);
            if (initial == PermissionRequestStatus.Applied) request.Apply();
        }
        var originalReason = request.RejectionReason;

        var result = await scenario.Service.Resolve(request.Id, approve, "Nuevo motivo");

        Assert.Equal(Failure.Conflict, result.Failure);
        Assert.Null(result.Value);
        Assert.Equal(initial, request.Status);
        Assert.Equal(originalResolver, request.ResolverId);
        Assert.Equal(originalTime, request.ResolvedUtc);
        Assert.Equal(originalReason, request.RejectionReason);
        Assert.Equal(0, scenario.Store.SaveCount);
        Assert.False(await scenario.Access.HasPermission(request.RequesterId, request.Permission, default));
    }

    [Fact]
    public async Task RF_GP_06_ApproveAppliedRequest_DoesNotSaveAgain()
    {
        var scenario = new Scenario(true);
        var request = scenario.AddRequest(Guid.NewGuid());
        await scenario.Service.Resolve(request.Id, true);

        var result = await scenario.Service.Resolve(request.Id, true);

        Assert.Equal(Failure.Conflict, result.Failure);
        Assert.Equal(PermissionRequestStatus.Applied, request.Status);
        Assert.Equal(1, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RD_07_UnknownPermission_DoesNotCreateRequest()
    {
        var scenario = new Scenario(false);

        var result = await scenario.Service.Create("Administrator");

        Assert.Equal(Failure.Invalid, result.Failure);
        Assert.Empty(scenario.Store.Requests);
    }

    [Fact]
    public async Task RF_GP_04_Reject_DoesNotGrantPermission()
    {
        var scenario = new Scenario(true);
        var request = scenario.AddRequest(Guid.NewGuid());

        var result = await scenario.Service.Resolve(request.Id, false, "No requerido");

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal("Rejected", result.Value!.Status);
        Assert.Equal("No requerido", result.Value.RejectionReason);
        Assert.Equal(scenario.Actor.Get()!.Id, result.Value.ResolverId);
        Assert.Equal(scenario.Clock.GetUtcNow(), result.Value.ResolvedUtc);
        Assert.Equal(1, scenario.Store.SaveCount);
        Assert.False(await scenario.Access.HasPermission(request.RequesterId, request.Permission, default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RF_GP_04_MissingReason_DoesNotSaveResolution(string? reason)
    {
        var scenario = new Scenario(true);
        var request = scenario.AddRequest(Guid.NewGuid());

        var result = await scenario.Service.Resolve(request.Id, false, reason);

        Assert.Equal(Failure.Invalid, result.Failure);
        Assert.Null(result.Value);
        Assert.Equal(PermissionRequestStatus.Pending, request.Status);
        Assert.Null(request.ResolverId);
        Assert.Null(request.ResolvedUtc);
        Assert.Null(request.RejectionReason);
        Assert.Equal(0, scenario.Store.SaveCount);
        Assert.False(await scenario.Access.HasPermission(request.RequesterId, request.Permission, default));
    }

    [Fact]
    public async Task RF_GP_02_AdministratorListsOnlyPendingRequests()
    {
        var scenario = new Scenario(true);
        var pending = scenario.AddRequest(Guid.NewGuid());
        var rejected = scenario.AddRequest(Guid.NewGuid());
        rejected.Reject(scenario.Actor.Get()!.Id, scenario.Clock.GetUtcNow(), "Motivo");
        var approved = scenario.AddRequest(Guid.NewGuid());
        approved.Approve(scenario.Actor.Get()!.Id, scenario.Clock.GetUtcNow());
        var applied = scenario.AddRequest(Guid.NewGuid());
        applied.Approve(scenario.Actor.Get()!.Id, scenario.Clock.GetUtcNow());
        applied.Apply();

        var result = await scenario.Service.List(true);

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(pending.Id, Assert.Single(result.Value!).Id);
    }

    [Fact]
    public async Task RD_06_MissingActor_RejectsCreation()
    {
        var scenario = new Scenario(false);
        scenario.Actor.Value = null;

        var result = await scenario.Service.Create(WorkOrderQuery.ReadPermission);

        Assert.Equal(Failure.Unauthorized, result.Failure);
        Assert.Empty(scenario.Store.Requests);
    }

    private sealed class Scenario
    {
        public FakeActor Actor { get; }
        public FakeStore Store { get; } = new();
        public FakeAccess Access { get; } = new();
        public FixedClock Clock { get; } = new();
        public PermissionRequestService Service { get; }

        public Scenario(bool administrator)
        {
            Actor = new FakeActor { Value = new Actor(Guid.NewGuid(), administrator) };
            Service = new PermissionRequestService(Store, Actor, Access, new FakeCatalog(), Clock);
        }

        public PermissionRequest AddRequest(Guid requester)
        {
            var request = PermissionRequest.Create(requester, WorkOrderQuery.ReadPermission, Clock.GetUtcNow());
            Store.Add(request);
            return request;
        }
    }

    private sealed class FakeActor : ICurrentActor
    {
        public Actor? Value { get; set; }
        public Actor? Get() => Value;
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeCatalog : IPermissionCatalog
    {
        public bool Contains(string permission) => permission == WorkOrderQuery.ReadPermission;
    }

    private sealed class FakeStore : IPermissionRequestStore
    {
        public List<PermissionRequest> Requests { get; } = [];
        public int SaveCount { get; private set; }
        public void Add(PermissionRequest request) => Requests.Add(request);
        public Task<PermissionRequest?> Find(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Requests.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<PermissionRequest>> List(Guid? requesterId, bool pendingOnly,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PermissionRequest>>(Requests
                .Where(x => requesterId is null || x.RequesterId == requesterId)
                .Where(x => !pendingOnly || x.Status == PermissionRequestStatus.Pending).ToArray());
        public Task<bool> Save(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeAccess : IAccessPermissions
    {
        private readonly HashSet<(Guid UserId, string Permission)> grants = [];
        public Task<bool> HasPermission(Guid userId, string permission, CancellationToken cancellationToken) =>
            Task.FromResult(grants.Contains((userId, permission)));
        public Task StageGrant(Guid userId, string permission, CancellationToken cancellationToken)
        {
            grants.Add((userId, permission));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOrderReader : IWorkOrderReader
    {
        public bool WasRead { get; private set; }
        public Task<IReadOnlyList<WorkOrderView>> List(CancellationToken cancellationToken)
        {
            WasRead = true;
            return Task.FromResult<IReadOnlyList<WorkOrderView>>([]);
        }
    }
}
