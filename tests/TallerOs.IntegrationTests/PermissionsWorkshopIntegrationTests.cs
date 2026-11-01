using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TallerOs.Api.Access;
using TallerOs.Api.Data;
using TallerOs.Api.Infrastructure.Access;
using TallerOs.Api.Infrastructure.Permissions;
using TallerOs.Api.Infrastructure.Workshop;
using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Application.Permissions;
using TallerOs.Application.Workshop;
using TallerOs.Domain.Permissions;

namespace TallerOs.IntegrationTests;

public sealed class PermissionsWorkshopIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RF_NEG_06_RF_GP_05_ApprovalUnlocksRealWorkshopOperation(bool detail)
    {
        await using var scenario = await IntegrationScenario.Create();
        var order = await scenario.PrepareOrder();
        var request = await scenario.As(scenario.RequesterId, async services =>
            await services.GetRequiredService<PermissionRequestService>().Create(WorkOrderQuery.ReadPermission));
        Assert.Equal(Failure.None, request.Failure);
        Assert.Equal("Pending", request.Value!.Status);
        var beforeApproval = await scenario.As(scenario.RequesterId, services => ReadOrder(services, order.Id, detail));
        Assert.Equal(Failure.Forbidden, beforeApproval.Failure);
        Assert.Null(beforeApproval.Order);

        var approval = await scenario.As(scenario.AdministratorId, async services =>
            await services.GetRequiredService<PermissionRequestService>().Resolve(request.Value.Id, true));
        var afterApproval = await scenario.As(scenario.RequesterId, services => ReadOrder(services, order.Id, detail));

        Assert.Equal(Failure.None, approval.Failure);
        Assert.Equal("Applied", approval.Value!.Status);
        Assert.Equal(scenario.AdministratorId, approval.Value.ResolverId);
        Assert.NotNull(approval.Value.ResolvedUtc);
        Assert.Equal(Failure.None, afterApproval.Failure);
        Assert.Equal(new WorkOrderView(order.Id, order.VehicleId, order.Reason, "Received"), afterApproval.Order);
        await scenario.As(scenario.RequesterId, async services =>
        {
            var db = services.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.RequesterId);
            var persisted = await db.PermissionRequests.AsNoTracking().SingleAsync(x => x.Id == request.Value.Id);
            var grant = await db.UserPermissions.AsNoTracking().SingleAsync(x =>
                x.UserId == scenario.RequesterId && x.Permission == WorkOrderQuery.ReadPermission);
            Assert.Equal(UserRole.Standard, user.Role);
            Assert.Equal(PermissionRequestStatus.Applied, persisted.Status);
            Assert.Equal(scenario.AdministratorId, persisted.ResolverId);
            Assert.Equal(scenario.RequesterId, grant.UserId);
            return true;
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RF_NEG_06_RF_GP_04_RejectionKeepsRealWorkshopOperationForbidden(bool detail)
    {
        await using var scenario = await IntegrationScenario.Create();
        var order = await scenario.PrepareOrder();
        var request = await scenario.As(scenario.RequesterId, async services =>
            await services.GetRequiredService<PermissionRequestService>().Create(WorkOrderQuery.ReadPermission));
        Assert.Equal(Failure.None, request.Failure);
        var beforeRejection = await scenario.As(scenario.RequesterId, services => ReadOrder(services, order.Id, detail));
        Assert.Equal(Failure.Forbidden, beforeRejection.Failure);
        Assert.Null(beforeRejection.Order);

        var rejection = await scenario.As(scenario.AdministratorId, async services =>
            await services.GetRequiredService<PermissionRequestService>().Resolve(request.Value!.Id, false, "Acceso no requerido"));
        var afterRejection = await scenario.As(scenario.RequesterId, services => ReadOrder(services, order.Id, detail));

        Assert.Equal(Failure.None, rejection.Failure);
        Assert.Equal("Rejected", rejection.Value!.Status);
        Assert.Equal("Acceso no requerido", rejection.Value.RejectionReason);
        Assert.Equal(Failure.Forbidden, afterRejection.Failure);
        Assert.Null(afterRejection.Order);
        await scenario.As(scenario.RequesterId, async services =>
        {
            var db = services.GetRequiredService<AppDbContext>();
            Assert.Empty(await db.UserPermissions.Where(x => x.UserId == scenario.RequesterId).ToArrayAsync());
            var persisted = await db.PermissionRequests.AsNoTracking().SingleAsync(x => x.Id == request.Value!.Id);
            Assert.Equal(PermissionRequestStatus.Rejected, persisted.Status);
            Assert.Equal("Acceso no requerido", persisted.RejectionReason);
            var workOrder = await db.WorkOrders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
            Assert.Equal(order.Status, workOrder.Status.ToString());
            return true;
        });
    }

    private static async Task<(Failure Failure, WorkOrderView? Order)> ReadOrder(IServiceProvider services,
        Guid id, bool detail)
    {
        var query = services.GetRequiredService<WorkOrderQuery>();
        if (detail)
        {
            var result = await query.Find(id);
            return (result.Failure, result.Value is { } value
                ? new WorkOrderView(value.Id, value.VehicleId, value.Reason, value.Status) : null);
        }

        var list = await query.List();
        return (list.Failure, list.Value?.SingleOrDefault(x => x.Id == id));
    }

    private sealed class IntegrationScenario(SqliteConnection connection, ServiceProvider provider,
        Guid requesterId, Guid administratorId) : IAsyncDisposable
    {
        public Guid RequesterId { get; } = requesterId;
        public Guid AdministratorId { get; } = administratorId;

        public static async Task<IntegrationScenario> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentActor, HttpCurrentActor>();
            services.AddScoped<IAccessPermissions, AccessPermissions>();
            services.AddSingleton<IPermissionCatalog>(new ConfiguredPermissionCatalog([WorkOrderQuery.ReadPermission]));
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<IPermissionRequestStore, PermissionRequestStore>();
            services.AddScoped<PermissionRequestService>();
            services.AddScoped<ICustomerStore, CustomerStore>();
            services.AddScoped<IVehicleStore, VehicleStore>();
            services.AddScoped<IWorkOrderStore, WorkOrderStore>();
            services.AddScoped<IWorkOrderReader, WorkOrderReader>();
            services.AddScoped<CustomerService>();
            services.AddScoped<VehicleService>();
            services.AddScoped<WorkOrderService>();
            services.AddScoped<WorkOrderQuery>();
            var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true, ValidateOnBuild = true
            });
            try
            {
                using var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                var requester = NewUser(UserRole.Standard, "requester@example.test");
                var administrator = NewUser(UserRole.Administrator, "administrator@example.test");
                db.Users.AddRange(requester, administrator);
                await db.SaveChangesAsync();
                return new IntegrationScenario(connection, provider, requester.Id, administrator.Id);
            }
            catch
            {
                await provider.DisposeAsync();
                await connection.DisposeAsync();
                throw;
            }
        }

        public async Task<T> As<T>(Guid userId, Func<IServiceProvider, Task<T>> operation)
        {
            using var scope = provider.CreateScope();
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId);
            var accessor = services.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext { RequestServices = services };
            accessor.HttpContext.Items["user"] = user;
            try { return await operation(services); }
            finally { accessor.HttpContext = null; }
        }

        public Task<WorkOrderDetailsView> PrepareOrder() => As(AdministratorId, async services =>
        {
            var customer = await services.GetRequiredService<CustomerService>().Create("Cliente ficticio", "8090000000");
            Assert.Equal(Failure.None, customer.Failure);
            var vehicle = await services.GetRequiredService<VehicleService>().Create(customer.Value!.Id, "PRUEBA-1", "Motocicleta");
            Assert.Equal(Failure.None, vehicle.Failure);
            var order = await services.GetRequiredService<WorkOrderService>().Receive(vehicle.Value!.Id, "Ruido en el motor");
            Assert.Equal(Failure.None, order.Failure);
            Assert.NotNull(order.Value);
            return order.Value;
        });

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            await connection.DisposeAsync();
        }

        private static UserAccount NewUser(UserRole role, string email)
        {
            var user = new UserAccount { Name = "Usuario ficticio", Email = email, Role = role, EmailConfirmed = true };
            user.PasswordHash = new PasswordHasher<UserAccount>().HashPassword(user, AccessService.NewSecret());
            return user;
        }
    }
}
