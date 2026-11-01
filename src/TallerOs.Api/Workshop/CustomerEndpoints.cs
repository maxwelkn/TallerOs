using TallerOs.Api.Http;
using TallerOs.Application.Common;
using TallerOs.Application.Workshop;

namespace TallerOs.Api.Workshop;

public sealed record CreateCustomerRequest(string? Name, string? Phone);

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var customers = app.MapGroup("/api/workshop/customers");
        customers.MapPost("", async (CreateCustomerRequest request, CustomerService service,
            CancellationToken cancellationToken) =>
        {
            var outcome = await service.Create(request.Name, request.Phone, cancellationToken);
            return outcome.Failure == Failure.None && outcome.Value is { } value
                ? Results.Created("/api/workshop/customers", value) : outcome.ToHttp();
        });
        customers.MapGet("", async (CustomerService service, CancellationToken cancellationToken) =>
            (await service.List(cancellationToken)).ToHttp());
    }
}
