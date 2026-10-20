using TallerOs.Api.Http;
using TallerOs.Application.Workshop;

namespace TallerOs.Api.Workshop;

public static class WorkOrderEndpoints
{
    public static void MapWorkOrderEndpoints(this WebApplication app) =>
        app.MapGet("/api/workshop/orders", async (WorkOrderQuery query, CancellationToken cancellationToken) =>
            (await query.List(cancellationToken)).ToHttp());
}
