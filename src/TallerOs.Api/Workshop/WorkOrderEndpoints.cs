using TallerOs.Api.Http;
using TallerOs.Application.Common;
using TallerOs.Application.Workshop;

namespace TallerOs.Api.Workshop;

public sealed record ReceiveWorkOrderRequest(Guid VehicleId, string? Reason);
public sealed record AddDiagnosisRequest(string? Description);
public sealed record TransitionWorkOrderRequest(string? Status, bool CustomerAuthorizationConfirmed = false,
    bool WorkCompletionConfirmed = false, bool DeliveryConfirmed = false);

public static class WorkOrderEndpoints
{
    public static void MapWorkOrderEndpoints(this WebApplication app)
    {
        var orders = app.MapGroup("/api/workshop/orders");
        orders.MapPost("", async (ReceiveWorkOrderRequest request, WorkOrderService service,
            CancellationToken cancellationToken) =>
        {
            var outcome = await service.Receive(request.VehicleId, request.Reason, cancellationToken);
            return outcome.Failure == Failure.None && outcome.Value is { } value
                ? Results.Created($"/api/workshop/orders/{value.Id}", value) : outcome.ToHttp();
        });
        orders.MapGet("", async (WorkOrderQuery query, CancellationToken cancellationToken) =>
            (await query.List(cancellationToken)).ToHttp());
        orders.MapGet("/{id:guid}", async (Guid id, WorkOrderQuery query, CancellationToken cancellationToken) =>
            (await query.Find(id, cancellationToken)).ToHttp());
        orders.MapGet("/{id:guid}/delivery-review", async (Guid id, HttpResponse response,
            DeliveryReviewService service, CancellationToken cancellationToken) =>
        {
            response.Headers.CacheControl = "no-store";
            return (await service.Review(id, cancellationToken)).ToHttp();
        });
        orders.MapPost("/{id:guid}/diagnoses", async (Guid id, AddDiagnosisRequest request,
            WorkOrderService service, CancellationToken cancellationToken) =>
        {
            var outcome = await service.AddDiagnosis(id, request.Description, cancellationToken);
            return outcome.Failure == Failure.None && outcome.Value is { } value
                ? Results.Created($"/api/workshop/orders/{id}", value) : outcome.ToHttp();
        });
        orders.MapPost("/{id:guid}/transitions", async (Guid id, TransitionWorkOrderRequest request,
            WorkOrderService service, CancellationToken cancellationToken) =>
            (await service.Transition(id, request.Status, request.CustomerAuthorizationConfirmed,
                request.WorkCompletionConfirmed, request.DeliveryConfirmed, cancellationToken)).ToHttp());
    }
}
