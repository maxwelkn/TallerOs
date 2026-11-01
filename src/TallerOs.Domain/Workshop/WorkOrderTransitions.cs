namespace TallerOs.Domain.Workshop;

public static class WorkOrderTransitions
{
    public static bool IsAllowed(WorkOrderStatus from, WorkOrderStatus to) => (from, to) is
        (WorkOrderStatus.Received, WorkOrderStatus.Diagnosed) or
        (WorkOrderStatus.Diagnosed, WorkOrderStatus.Authorized) or
        (WorkOrderStatus.Authorized, WorkOrderStatus.InRepair) or
        (WorkOrderStatus.InRepair, WorkOrderStatus.Delivered);

    public static void EnsureAllowed(WorkOrderStatus from, WorkOrderStatus to, bool hasDiagnosis,
        bool customerAuthorizationConfirmed, bool workCompletionConfirmed, bool deliveryConfirmed)
    {
        if (!IsAllowed(from, to))
            throw new WorkshopRuleException($"Transición de {from} a {to} no permitida.");
        if (to == WorkOrderStatus.Diagnosed && !hasDiagnosis)
            throw new WorkshopRuleException("Registra un diagnóstico antes de avanzar la orden.");
        if (to == WorkOrderStatus.Authorized && !customerAuthorizationConfirmed)
            throw new WorkshopRuleException("Confirma la autorización del cliente antes de avanzar la orden.");
        if (to == WorkOrderStatus.Delivered && (!workCompletionConfirmed || !deliveryConfirmed))
            throw new WorkshopRuleException("Confirma el trabajo completado y la entrega del vehículo.");
    }
}
