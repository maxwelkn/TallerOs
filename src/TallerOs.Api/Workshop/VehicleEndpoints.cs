using TallerOs.Api.Http;
using TallerOs.Application.Common;
using TallerOs.Application.Workshop;

namespace TallerOs.Api.Workshop;

public sealed record CreateVehicleRequest(Guid CustomerId, string? LicensePlate, string? Description);

public static class VehicleEndpoints
{
    public static void MapVehicleEndpoints(this WebApplication app)
    {
        var vehicles = app.MapGroup("/api/workshop/vehicles");
        vehicles.MapPost("", async (CreateVehicleRequest request, VehicleService service,
            CancellationToken cancellationToken) =>
        {
            var outcome = await service.Create(request.CustomerId, request.LicensePlate, request.Description,
                cancellationToken);
            return outcome.Failure == Failure.None && outcome.Value is { } value
                ? Results.Created("/api/workshop/vehicles", value) : outcome.ToHttp();
        });
        vehicles.MapGet("", async (VehicleService service, CancellationToken cancellationToken) =>
            (await service.List(cancellationToken)).ToHttp());
    }
}
