using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Application.Workshop;
using TallerOs.Domain.Workshop;

namespace TallerOs.UnitTests;

public sealed class WorkshopServiceTests
{
    [Theory]
    [InlineData("create-customer")]
    [InlineData("list-customers")]
    [InlineData("create-vehicle")]
    [InlineData("list-vehicles")]
    [InlineData("receive")]
    [InlineData("diagnosis")]
    [InlineData("transition")]
    [InlineData("list-orders")]
    [InlineData("find-order")]
    public async Task RF_NEG_06_MissingActor_DeniesOperationWithoutAccessingStore(string operation)
    {
        var scenario = new Scenario();
        scenario.Actor.Value = null;

        var failure = await scenario.Run(operation);

        Assert.Equal(Failure.Unauthorized, failure);
        Assert.Equal(0, scenario.Store.ReadCount);
        Assert.Equal(0, scenario.Store.SaveCount);
        Assert.Single(scenario.Store.Customers);
        Assert.Single(scenario.Store.Vehicles);
        Assert.Single(scenario.Store.Orders);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RF_NEG_02_CreateCustomer_ReturnsNormalizedView(bool administrator)
    {
        var scenario = new Scenario(administrator);

        var result = await scenario.Customers.Create("  Otro cliente  ", "  8090000001  ");

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal("Otro cliente", result.Value!.Name);
        Assert.Equal("8090000001", result.Value.Phone);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal(1, scenario.Store.SaveCount);
    }

    [Theory]
    [InlineData(null, "8090000000")]
    [InlineData("   ", "8090000000")]
    [InlineData("Cliente", null)]
    public async Task RD_07_InvalidCustomer_DoesNotAddOrSave(string? name, string? phone)
    {
        var scenario = new Scenario();

        var result = await scenario.Customers.Create(name, phone);

        Assert.Equal(Failure.Invalid, result.Failure);
        Assert.Null(result.Value);
        Assert.Single(scenario.Store.Customers);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RF_NEG_02_CreateVehicle_AssociatesExistingCustomer(bool administrator)
    {
        var scenario = new Scenario(administrator);

        var result = await scenario.Vehicles.Create(scenario.Customer.Id, "  PRUEBA-2  ", "  Automóvil  ");

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(scenario.Customer.Id, result.Value!.CustomerId);
        Assert.Equal("PRUEBA-2", result.Value.LicensePlate);
        Assert.Equal("Automóvil", result.Value.Description);
        Assert.Equal(1, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RD_07_CreateVehicleWithMissingCustomer_DoesNotAddOrSave()
    {
        var scenario = new Scenario();

        var result = await scenario.Vehicles.Create(Guid.NewGuid(), "PRUEBA-2", "Automóvil");

        Assert.Equal(Failure.NotFound, result.Failure);
        Assert.Single(scenario.Store.Vehicles);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RF_NEG_02_ReceiveOrder_StartsReceivedForExistingVehicle(bool administrator)
    {
        var scenario = new Scenario(administrator);

        var result = await scenario.Orders.Receive(scenario.Vehicle.Id, "  Ruido en el motor  ");

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(scenario.Vehicle.Id, result.Value!.VehicleId);
        Assert.Equal("Ruido en el motor", result.Value.Reason);
        Assert.Equal("Received", result.Value.Status);
        Assert.Empty(result.Value.Diagnoses);
        Assert.Equal(1, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RD_07_ReceiveOrderWithMissingVehicle_DoesNotAddOrSave()
    {
        var scenario = new Scenario();

        var result = await scenario.Orders.Receive(Guid.NewGuid(), "Ruido");

        Assert.Equal(Failure.NotFound, result.Failure);
        Assert.Single(scenario.Store.Orders);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Theory]
    [InlineData("vehicle-empty-id")]
    [InlineData("vehicle-empty-plate")]
    [InlineData("vehicle-empty-description")]
    [InlineData("order-empty-id")]
    [InlineData("order-empty-reason")]
    public async Task RD_07_InvalidCreation_DoesNotReadOrSave(string operation)
    {
        var scenario = new Scenario();

        var failure = operation switch
        {
            "vehicle-empty-id" => (await scenario.Vehicles.Create(Guid.Empty, "PRUEBA-2", "Automóvil")).Failure,
            "vehicle-empty-plate" => (await scenario.Vehicles.Create(scenario.Customer.Id, " ", "Automóvil")).Failure,
            "vehicle-empty-description" => (await scenario.Vehicles.Create(scenario.Customer.Id, "PRUEBA-2", null)).Failure,
            "order-empty-id" => (await scenario.Orders.Receive(Guid.Empty, "Ruido")).Failure,
            _ => (await scenario.Orders.Receive(scenario.Vehicle.Id, " ")).Failure
        };

        Assert.Equal(Failure.Invalid, failure);
        Assert.Equal(0, scenario.Store.ReadCount);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RF_NEG_02_ListCustomers_ReturnsStoredCustomerView()
    {
        var scenario = new Scenario();

        var result = await scenario.Customers.List();

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(scenario.Customer.Id, Assert.Single(result.Value!).Id);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RF_NEG_02_ListVehicles_ReturnsStoredVehicleView()
    {
        var scenario = new Scenario();

        var result = await scenario.Vehicles.List();

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(scenario.Vehicle.Id, Assert.Single(result.Value!).Id);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RF_NEG_02_AddDiagnosis_DoesNotChangeOrderState()
    {
        var scenario = new Scenario();

        var result = await scenario.Orders.AddDiagnosis(scenario.Order.Id, "  Revisar transmisión  ");

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(scenario.Order.Id, result.Value!.WorkOrderId);
        Assert.Equal("Revisar transmisión", result.Value.Description);
        Assert.Equal(WorkOrderStatus.Received, scenario.Order.Status);
        Assert.Equal(1, scenario.Store.SaveCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RD_07_InvalidDiagnosis_DoesNotReadOrSave(string? description)
    {
        var scenario = new Scenario();

        var result = await scenario.Orders.AddDiagnosis(scenario.Order.Id, description);

        Assert.Equal(Failure.Invalid, result.Failure);
        Assert.Empty(scenario.Order.Diagnoses);
        Assert.Equal(0, scenario.Store.ReadCount);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Theory]
    [InlineData("diagnosis")]
    [InlineData("transition")]
    public async Task RD_07_MissingOrder_DoesNotSave(string operation)
    {
        var scenario = new Scenario();
        var missing = Guid.NewGuid();

        var failure = operation == "diagnosis"
            ? (await scenario.Orders.AddDiagnosis(missing, "Revisar transmisión")).Failure
            : (await scenario.Orders.Transition(missing, "Diagnosed")).Failure;

        Assert.Equal(Failure.NotFound, failure);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("999")]
    [InlineData("Pending")]
    [InlineData("Diagnosed, Authorized")]
    public async Task RD_07_UnknownState_DoesNotReadOrSave(string? state)
    {
        var scenario = new Scenario();

        var result = await scenario.Orders.Transition(scenario.Order.Id, state, true, true, true);

        Assert.Equal(Failure.Invalid, result.Failure);
        Assert.Equal(WorkOrderStatus.Received, scenario.Order.Status);
        Assert.Equal(0, scenario.Store.ReadCount);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RF_NEG_03_TransitionWithoutDiagnosis_DoesNotSave()
    {
        var scenario = new Scenario();

        var result = await scenario.Orders.Transition(scenario.Order.Id, "Diagnosed");

        Assert.Equal(Failure.Conflict, result.Failure);
        Assert.Equal(WorkOrderStatus.Received, scenario.Order.Status);
        Assert.Empty(scenario.Order.Diagnoses);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RF_NEG_04_ForbiddenTransition_DoesNotSave()
    {
        var scenario = new Scenario();
        scenario.Order.AddDiagnosis("Revisar transmisión");

        var result = await scenario.Orders.Transition(scenario.Order.Id, "InRepair", true, true, true);

        Assert.Equal(Failure.Conflict, result.Failure);
        Assert.Equal(WorkOrderStatus.Received, scenario.Order.Status);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RF_NEG_03_ValidTransition_ReturnsUpdatedView()
    {
        var scenario = new Scenario();
        scenario.Order.AddDiagnosis("Revisar transmisión");

        var result = await scenario.Orders.Transition(scenario.Order.Id, " diagnosed ");

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal("Diagnosed", result.Value!.Status);
        Assert.Single(result.Value.Diagnoses);
        Assert.Equal(1, scenario.Store.SaveCount);
    }

    [Theory]
    [InlineData("create-customer")]
    [InlineData("create-vehicle")]
    [InlineData("receive")]
    [InlineData("diagnosis")]
    [InlineData("transition")]
    public async Task RD_09_StorageConflict_DoesNotReturnSuccess(string operation)
    {
        var scenario = new Scenario();
        scenario.Store.SaveResult = false;
        if (operation == "transition") scenario.Order.AddDiagnosis("Revisar transmisión");

        var failure = await scenario.Run(operation);

        Assert.Equal(Failure.Conflict, failure);
        Assert.Equal(1, scenario.Store.SaveCount);
    }

    [Theory]
    [InlineData("list-orders")]
    [InlineData("find-order")]
    public async Task RF_NEG_06_StandardWithoutReadPermission_DoesNotReadOrders(string operation)
    {
        var scenario = new Scenario();
        scenario.Access.Granted = false;

        var failure = await scenario.Run(operation);

        Assert.Equal(Failure.Forbidden, failure);
        Assert.Equal(0, scenario.Store.ReadCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RF_NEG_06_AllowedReader_ReturnsOrderDetails(bool administrator)
    {
        var scenario = new Scenario(administrator);
        scenario.Access.Granted = !administrator;
        scenario.Order.AddDiagnosis("Diagnóstico de prueba");

        var result = await scenario.Query.Find(scenario.Order.Id);

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(scenario.Order.Id, result.Value!.Id);
        Assert.Equal("Diagnóstico de prueba", Assert.Single(result.Value.Diagnoses).Description);
        Assert.Equal(administrator ? 0 : 1, scenario.Access.CheckCount);
    }

    [Fact]
    public async Task RD_07_FindMissingOrder_ReturnsNotFound()
    {
        var scenario = new Scenario();

        var result = await scenario.Query.Find(Guid.NewGuid());

        Assert.Equal(Failure.NotFound, result.Failure);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("diagnosis")]
    [InlineData("transition")]
    [InlineData("find-order")]
    public async Task RD_07_EmptyOrderIdentifier_DoesNotReadStore(string operation)
    {
        var scenario = new Scenario();

        var failure = operation switch
        {
            "diagnosis" => (await scenario.Orders.AddDiagnosis(Guid.Empty, "Revisar")).Failure,
            "transition" => (await scenario.Orders.Transition(Guid.Empty, "Diagnosed")).Failure,
            _ => (await scenario.Query.Find(Guid.Empty)).Failure
        };

        Assert.Equal(Failure.Invalid, failure);
        Assert.Equal(0, scenario.Store.ReadCount);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RD_06_EmptyActorIdentifier_DoesNotReadStore()
    {
        var scenario = new Scenario();
        scenario.Actor.Value = new Actor(Guid.Empty, true);

        var result = await scenario.Customers.List();

        Assert.Equal(Failure.Unauthorized, result.Failure);
        Assert.Equal(0, scenario.Store.ReadCount);
    }

    [Fact]
    public async Task RF_NEG_02_CreateCustomer_PropagatesCancellationToken()
    {
        var scenario = new Scenario();
        using var cancellation = new CancellationTokenSource();

        var result = await scenario.Customers.Create("Otro cliente", "8090000001", cancellation.Token);

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(cancellation.Token, scenario.Store.LastToken);
    }

    private sealed class Scenario
    {
        public FakeActor Actor { get; }
        public FakeStore Store { get; } = new();
        public FakeAccess Access { get; } = new();
        public Customer Customer { get; }
        public Vehicle Vehicle { get; }
        public WorkOrder Order { get; }
        public CustomerService Customers { get; }
        public VehicleService Vehicles { get; }
        public WorkOrderService Orders { get; }
        public WorkOrderQuery Query { get; }

        public Scenario(bool administrator = false)
        {
            Actor = new FakeActor { Value = new Actor(Guid.NewGuid(), administrator) };
            Customer = Customer.Create("Cliente de prueba", "8090000000");
            Vehicle = Vehicle.Create(Customer.Id, "PRUEBA-1", "Motocicleta");
            Order = WorkOrder.Create(Vehicle.Id, "Ruido");
            Store.Customers.Add(Customer);
            Store.Vehicles.Add(Vehicle);
            Store.Orders.Add(Order);
            Customers = new CustomerService(Store, Actor);
            Vehicles = new VehicleService(Store, Store, Actor);
            Orders = new WorkOrderService(Store, Store, Actor);
            Query = new WorkOrderQuery(Actor, Access, Store);
        }

        public async Task<Failure> Run(string operation) => operation switch
        {
            "create-customer" => (await Customers.Create("Otro cliente", "8090000001")).Failure,
            "list-customers" => (await Customers.List()).Failure,
            "create-vehicle" => (await Vehicles.Create(Customer.Id, "PRUEBA-2", "Automóvil")).Failure,
            "list-vehicles" => (await Vehicles.List()).Failure,
            "receive" => (await Orders.Receive(Vehicle.Id, "Ruido")).Failure,
            "diagnosis" => (await Orders.AddDiagnosis(Order.Id, "Revisar transmisión")).Failure,
            "transition" => (await Orders.Transition(Order.Id, "Diagnosed")).Failure,
            "list-orders" => (await Query.List()).Failure,
            _ => (await Query.Find(Order.Id)).Failure
        };
    }

    private sealed class FakeActor : ICurrentActor
    {
        public Actor? Value { get; set; }
        public Actor? Get() => Value;
    }

    private sealed class FakeAccess : IAccessPermissions
    {
        public bool Granted { get; set; } = true;
        public int CheckCount { get; private set; }
        public Task<bool> HasPermission(Guid userId, string permission, CancellationToken cancellationToken)
        {
            CheckCount++;
            return Task.FromResult(Granted && permission == WorkOrderQuery.ReadPermission);
        }
        public Task StageGrant(Guid userId, string permission, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeStore : ICustomerStore, IVehicleStore, IWorkOrderStore, IWorkOrderReader
    {
        public List<Customer> Customers { get; } = [];
        public List<Vehicle> Vehicles { get; } = [];
        public List<WorkOrder> Orders { get; } = [];
        public bool SaveResult { get; set; } = true;
        public int SaveCount { get; private set; }
        public int ReadCount { get; private set; }
        public CancellationToken LastToken { get; private set; }

        Task<bool> ICustomerStore.Exists(Guid id, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(Customers.Any(x => x.Id == id));
        }
        Task<bool> IVehicleStore.Exists(Guid id, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(Vehicles.Any(x => x.Id == id));
        }
        Task<IReadOnlyList<Customer>> ICustomerStore.List(CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<IReadOnlyList<Customer>>(Customers.ToArray());
        }
        Task<IReadOnlyList<Vehicle>> IVehicleStore.List(CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<IReadOnlyList<Vehicle>>(Vehicles.ToArray());
        }
        Task<WorkOrder?> IWorkOrderStore.Find(Guid id, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(Orders.FirstOrDefault(x => x.Id == id));
        }
        Task<IReadOnlyList<WorkOrderView>> IWorkOrderReader.List(CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<IReadOnlyList<WorkOrderView>>(Orders.Select(x =>
                new WorkOrderView(x.Id, x.VehicleId, x.Reason, x.Status.ToString())).ToArray());
        }
        Task<WorkOrderDetailsView?> IWorkOrderReader.Find(Guid id, CancellationToken cancellationToken)
        {
            ReadCount++;
            var order = Orders.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(order is null ? null : WorkOrderDetailsView.From(order));
        }
        public void Add(Customer customer) => Customers.Add(customer);
        public void Add(Vehicle vehicle) => Vehicles.Add(vehicle);
        public void Add(WorkOrder order) => Orders.Add(order);
        public Task<bool> Save(CancellationToken cancellationToken)
        {
            SaveCount++;
            LastToken = cancellationToken;
            return Task.FromResult(SaveResult);
        }
    }
}
