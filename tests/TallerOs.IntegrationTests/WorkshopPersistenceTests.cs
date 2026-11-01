using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Api.Infrastructure.Workshop;
using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Application.Workshop;
using TallerOs.Domain.Workshop;

namespace TallerOs.IntegrationTests;

public sealed class WorkshopPersistenceTests
{
    [Fact]
    public async Task RD_09_WorkshopWorkflow_PersistsAcrossContexts()
    {
        await using var database = await DatabaseFixture.Create();
        var actor = new TestActor();
        Guid customerId;
        Guid vehicleId;
        Guid orderId;
        using (var db = database.OpenContext())
        {
            var customer = await new CustomerService(new CustomerStore(db), actor).Create("Cliente ficticio", "8090000000");
            Assert.Equal(Failure.None, customer.Failure);
            customerId = customer.Value!.Id;
        }
        using (var db = database.OpenContext())
        {
            var vehicle = await new VehicleService(new VehicleStore(db), new CustomerStore(db), actor)
                .Create(customerId, "PRUEBA-1", "Motocicleta");
            Assert.Equal(Failure.None, vehicle.Failure);
            vehicleId = vehicle.Value!.Id;
        }
        using (var db = database.OpenContext())
        {
            var order = await Service(db, actor).Receive(vehicleId, "Ruido en el motor");
            Assert.Equal(Failure.None, order.Failure);
            orderId = order.Value!.Id;
        }
        using (var db = database.OpenContext())
        {
            var diagnosis = await Service(db, actor).AddDiagnosis(orderId, "Revisar transmisión");
            Assert.Equal(Failure.None, diagnosis.Failure);
        }

        foreach (var status in new[] { "Diagnosed", "Authorized", "InRepair", "Delivered" })
        {
            using var db = database.OpenContext();
            var result = await Service(db, actor).Transition(orderId, status, true, true, true);
            Assert.Equal(Failure.None, result.Failure);
        }

        using var verification = database.OpenContext();
        var details = await new WorkOrderReader(verification).Find(orderId, default);
        Assert.NotNull(details);
        Assert.Equal("Delivered", details.Status);
        Assert.Equal("Revisar transmisión", Assert.Single(details.Diagnoses).Description);
        Assert.Equal(customerId, (await new CustomerStore(verification).List(default)).Single().Id);
        Assert.Equal(vehicleId, (await new VehicleStore(verification).List(default)).Single().Id);
        Assert.Equal(customerId, (await new VehicleStore(verification).List(default)).Single().CustomerId);
    }

    [Fact]
    public async Task RD_04_ConcurrentTransitions_DoNotOverwriteFirstResolution()
    {
        await using var database = await DatabaseFixture.Create();
        var orderId = await database.SeedOrder();
        using var first = database.OpenContext();
        using var second = database.OpenContext();
        var firstStore = new WorkOrderStore(first);
        var secondStore = new WorkOrderStore(second);
        var firstOrder = await firstStore.Find(orderId, default);
        var secondOrder = await secondStore.Find(orderId, default);
        firstOrder!.MoveTo(WorkOrderStatus.Diagnosed);
        secondOrder!.AddDiagnosis("Diagnóstico del contexto obsoleto");
        secondOrder.MoveTo(WorkOrderStatus.Diagnosed);

        var firstSaved = await firstStore.Save(default);
        var secondSaved = await secondStore.Save(default);

        Assert.True(firstSaved);
        Assert.False(secondSaved);
        using var verification = database.OpenContext();
        var persisted = await new WorkOrderStore(verification).Find(orderId, default);
        Assert.Equal(WorkOrderStatus.Diagnosed, persisted!.Status);
        Assert.Single(persisted.Diagnoses);
    }

    [Fact]
    public async Task RD_07_InvalidVehicleRelationship_IsRejectedByDatabase()
    {
        await using var database = await DatabaseFixture.Create();
        using var db = database.OpenContext();
        var store = new VehicleStore(db);
        store.Add(Vehicle.Create(Guid.NewGuid(), "PRUEBA-1", "Motocicleta"));

        var saved = await store.Save(default);

        Assert.False(saved);
        using var verification = database.OpenContext();
        Assert.Empty(await new VehicleStore(verification).List(default));
    }

    [Fact]
    public async Task RD_04_TransitionWithoutStoredDiagnosis_PreservesPersistedState()
    {
        await using var database = await DatabaseFixture.Create();
        var id = await database.SeedOrder(withDiagnosis: false);
        using var db = database.OpenContext();

        var result = await Service(db, new TestActor()).Transition(id, "Diagnosed");

        Assert.Equal(Failure.Conflict, result.Failure);
        using var verification = database.OpenContext();
        var order = await new WorkOrderStore(verification).Find(id, default);
        Assert.Equal(WorkOrderStatus.Received, order!.Status);
        Assert.Empty(order.Diagnoses);
    }

    [Fact]
    public async Task RD_09_WorkshopMapping_DoesNotRequireSchemaMigration()
    {
        await using var database = await DatabaseFixture.Create();
        using var db = database.OpenContext();

        var pendingChanges = db.Database.HasPendingModelChanges();

        Assert.False(pendingChanges);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task RD_09_StoredOrder_SurvivesClosingAndReopeningDatabaseFile()
    {
        var directory = Directory.CreateTempSubdirectory("talleros-persistence-");
        var path = Path.Combine(directory.FullName, "test.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False;Foreign Keys=True").Options;
        var orderId = Guid.Empty;
        try
        {
            using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync();
                var customer = Customer.Create("Cliente ficticio", "8090000000");
                var vehicle = Vehicle.Create(customer.Id, "PRUEBA-1", "Motocicleta");
                var order = WorkOrder.Create(vehicle.Id, "Ruido");
                order.AddDiagnosis("Diagnóstico persistente");
                order.MoveTo(WorkOrderStatus.Diagnosed);
                db.Customers.Add(customer);
                db.Vehicles.Add(vehicle);
                db.WorkOrders.Add(order);
                await db.SaveChangesAsync();
                orderId = order.Id;
            }

            using var reopened = new AppDbContext(options);
            var persisted = await new WorkOrderReader(reopened).Find(orderId, default);

            Assert.NotNull(persisted);
            Assert.Equal("Diagnosed", persisted.Status);
            Assert.Equal("Diagnóstico persistente", Assert.Single(persisted.Diagnoses).Description);
        }
        finally
        {
            foreach (var file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private static WorkOrderService Service(AppDbContext db, ICurrentActor actor) =>
        new(new WorkOrderStore(db), new VehicleStore(db), actor);

    private sealed class TestActor : ICurrentActor
    {
        public Actor? Get() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"), false);
    }

    private sealed class DatabaseFixture(SqliteConnection connection) : IAsyncDisposable
    {
        public static async Task<DatabaseFixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var fixture = new DatabaseFixture(connection);
            using var db = fixture.OpenContext();
            await db.Database.MigrateAsync();
            return fixture;
        }

        public AppDbContext OpenContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

        public async Task<Guid> SeedOrder(bool withDiagnosis = true)
        {
            using var db = OpenContext();
            var customer = Customer.Create("Cliente ficticio", "8090000000");
            var vehicle = Vehicle.Create(customer.Id, "PRUEBA-1", "Motocicleta");
            var order = WorkOrder.Create(vehicle.Id, "Ruido");
            if (withDiagnosis) order.AddDiagnosis("Revisar transmisión");
            db.Customers.Add(customer);
            db.Vehicles.Add(vehicle);
            db.WorkOrders.Add(order);
            await db.SaveChangesAsync();
            return order.Id;
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
