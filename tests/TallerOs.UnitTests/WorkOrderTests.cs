using TallerOs.Domain.Workshop;

namespace TallerOs.UnitTests;

public sealed class WorkOrderTests
{
    [Theory]
    [InlineData(WorkOrderStatus.Received, WorkOrderStatus.Diagnosed)]
    [InlineData(WorkOrderStatus.Diagnosed, WorkOrderStatus.Authorized)]
    [InlineData(WorkOrderStatus.Authorized, WorkOrderStatus.InRepair)]
    [InlineData(WorkOrderStatus.InRepair, WorkOrderStatus.Delivered)]
    public void RF_NEG_03_AllowedTransition_MovesToRequestedState(WorkOrderStatus initial, WorkOrderStatus next)
    {
        var order = CreateAt(initial);
        var diagnosis = Assert.Single(order.Diagnoses);

        order.MoveTo(next, true, true, true);

        Assert.Equal(next, order.Status);
        Assert.Same(diagnosis, Assert.Single(order.Diagnoses));
        Assert.Equal(order.Id, diagnosis.WorkOrderId);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Received, WorkOrderStatus.InRepair)]
    [InlineData(WorkOrderStatus.Received, WorkOrderStatus.Received)]
    [InlineData(WorkOrderStatus.Received, WorkOrderStatus.Authorized)]
    [InlineData(WorkOrderStatus.Received, WorkOrderStatus.Delivered)]
    [InlineData(WorkOrderStatus.Diagnosed, WorkOrderStatus.Received)]
    [InlineData(WorkOrderStatus.Diagnosed, WorkOrderStatus.Diagnosed)]
    [InlineData(WorkOrderStatus.Diagnosed, WorkOrderStatus.InRepair)]
    [InlineData(WorkOrderStatus.Diagnosed, WorkOrderStatus.Delivered)]
    [InlineData(WorkOrderStatus.Authorized, WorkOrderStatus.Received)]
    [InlineData(WorkOrderStatus.Authorized, WorkOrderStatus.Diagnosed)]
    [InlineData(WorkOrderStatus.Authorized, WorkOrderStatus.Authorized)]
    [InlineData(WorkOrderStatus.Authorized, WorkOrderStatus.Delivered)]
    [InlineData(WorkOrderStatus.InRepair, WorkOrderStatus.Received)]
    [InlineData(WorkOrderStatus.InRepair, WorkOrderStatus.Diagnosed)]
    [InlineData(WorkOrderStatus.InRepair, WorkOrderStatus.Authorized)]
    [InlineData(WorkOrderStatus.InRepair, WorkOrderStatus.InRepair)]
    [InlineData(WorkOrderStatus.Delivered, WorkOrderStatus.Received)]
    [InlineData(WorkOrderStatus.Delivered, WorkOrderStatus.Diagnosed)]
    [InlineData(WorkOrderStatus.Delivered, WorkOrderStatus.Authorized)]
    [InlineData(WorkOrderStatus.Delivered, WorkOrderStatus.InRepair)]
    [InlineData(WorkOrderStatus.Delivered, WorkOrderStatus.Delivered)]
    public void RF_NEG_04_RD_04_ForbiddenTransition_PreservesOrder(WorkOrderStatus initial, WorkOrderStatus next)
    {
        var order = CreateAt(initial);
        var original = OrderSnapshot.Capture(order);

        Assert.Throws<WorkshopRuleException>(() => order.MoveTo(next, true, true, true));

        original.AssertUnchanged(order);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Received)]
    [InlineData(WorkOrderStatus.Diagnosed)]
    [InlineData(WorkOrderStatus.Authorized)]
    [InlineData(WorkOrderStatus.InRepair)]
    [InlineData(WorkOrderStatus.Delivered)]
    public void RF_NEG_05_DeliveredOrder_CannotLeaveTerminalState(WorkOrderStatus next)
    {
        var order = CreateAt(WorkOrderStatus.Delivered);
        var original = OrderSnapshot.Capture(order);

        Assert.Throws<WorkshopRuleException>(() => order.MoveTo(next, true, true, true));

        original.AssertUnchanged(order);
    }

    [Fact]
    public void RF_NEG_03_DiagnoseWithoutDiagnosis_RejectsAndPreservesOrder()
    {
        var order = WorkOrder.Create(Guid.NewGuid(), "Ruido en el motor");
        var original = OrderSnapshot.Capture(order);

        Assert.Throws<WorkshopRuleException>(() => order.MoveTo(WorkOrderStatus.Diagnosed, true, true, true));

        original.AssertUnchanged(order);
    }

    [Fact]
    public void RF_NEG_03_AuthorizeWithoutCustomerConfirmation_RejectsAndPreservesOrder()
    {
        var order = CreateAt(WorkOrderStatus.Diagnosed);
        var original = OrderSnapshot.Capture(order);

        Assert.Throws<WorkshopRuleException>(() => order.MoveTo(WorkOrderStatus.Authorized));

        original.AssertUnchanged(order);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void RF_NEG_03_DeliverWithoutBothConfirmations_RejectsAndPreservesOrder(
        bool workCompletionConfirmed, bool deliveryConfirmed)
    {
        var order = CreateAt(WorkOrderStatus.InRepair);
        var original = OrderSnapshot.Capture(order);

        Assert.Throws<WorkshopRuleException>(() => order.MoveTo(WorkOrderStatus.Delivered,
            workCompletionConfirmed: workCompletionConfirmed, deliveryConfirmed: deliveryConfirmed));

        original.AssertUnchanged(order);
    }

    [Fact]
    public void RD_07_UnknownTargetState_RejectsAndPreservesOrder()
    {
        var order = CreateAt(WorkOrderStatus.Received);
        var original = OrderSnapshot.Capture(order);

        Assert.Throws<WorkshopRuleException>(() => order.MoveTo((WorkOrderStatus)999, true, true, true));

        original.AssertUnchanged(order);
    }

    [Fact]
    public void RF_NEG_03_Create_StartsReceivedWithoutDiagnoses()
    {
        var vehicleId = Guid.NewGuid();

        var order = WorkOrder.Create(vehicleId, "  Ruido en el motor  ");

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(vehicleId, order.VehicleId);
        Assert.Equal("Ruido en el motor", order.Reason);
        Assert.Equal(WorkOrderStatus.Received, order.Status);
        Assert.Empty(order.Diagnoses);
    }

    [Fact]
    public void RF_NEG_03_AddDiagnosis_RecordsDescriptionWithoutMovingOrder()
    {
        var order = WorkOrder.Create(Guid.NewGuid(), "Ruido en el motor");

        var diagnosis = order.AddDiagnosis("  Revisar transmisión  ");

        Assert.Same(diagnosis, Assert.Single(order.Diagnoses));
        Assert.NotEqual(Guid.Empty, diagnosis.Id);
        Assert.Equal(order.Id, diagnosis.WorkOrderId);
        Assert.Equal("Revisar transmisión", diagnosis.Description);
        Assert.Equal(WorkOrderStatus.Received, order.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RD_07_AddInvalidDiagnosis_PreservesOrder(string? description)
    {
        var order = CreateAt(WorkOrderStatus.Received);
        var original = OrderSnapshot.Capture(order);

        Assert.Throws<WorkshopRuleException>(() => order.AddDiagnosis(description));

        original.AssertUnchanged(order);
    }

    [Fact]
    public void RF_NEG_03_DiagnosesCollection_CannotBeChangedOutsideOrder()
    {
        var order = CreateAt(WorkOrderStatus.Received);
        var diagnoses = Assert.IsAssignableFrom<ICollection<Diagnosis>>(order.Diagnoses);
        var original = OrderSnapshot.Capture(order);

        Assert.Throws<NotSupportedException>(diagnoses.Clear);

        original.AssertUnchanged(order);
    }

    private static WorkOrder CreateAt(WorkOrderStatus status)
    {
        var order = WorkOrder.Create(Guid.NewGuid(), "Ruido en el motor");
        order.AddDiagnosis("Revisar transmisión");
        if (status == WorkOrderStatus.Received) return order;
        order.MoveTo(WorkOrderStatus.Diagnosed);
        if (status == WorkOrderStatus.Diagnosed) return order;
        order.MoveTo(WorkOrderStatus.Authorized, customerAuthorizationConfirmed: true);
        if (status == WorkOrderStatus.Authorized) return order;
        order.MoveTo(WorkOrderStatus.InRepair);
        if (status == WorkOrderStatus.InRepair) return order;
        order.MoveTo(WorkOrderStatus.Delivered, workCompletionConfirmed: true, deliveryConfirmed: true);
        return order;
    }

    private sealed record OrderSnapshot(Guid Id, Guid VehicleId, string Reason, WorkOrderStatus Status,
        (Guid Id, Guid OrderId, string Description)[] Diagnoses)
    {
        public static OrderSnapshot Capture(WorkOrder order) => new(order.Id, order.VehicleId, order.Reason,
            order.Status, order.Diagnoses.Select(x => (x.Id, x.WorkOrderId, x.Description)).ToArray());

        public void AssertUnchanged(WorkOrder order)
        {
            Assert.Equal(Id, order.Id);
            Assert.Equal(VehicleId, order.VehicleId);
            Assert.Equal(Reason, order.Reason);
            Assert.Equal(Status, order.Status);
            Assert.Equal(Diagnoses, order.Diagnoses.Select(x => (x.Id, x.WorkOrderId, x.Description)).ToArray());
        }
    }
}
