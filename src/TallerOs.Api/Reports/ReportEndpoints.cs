using TallerOs.Api.Http;
using TallerOs.Application.Reports;

namespace TallerOs.Api.Reports;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var reports = app.MapGroup("/api/reports");
        reports.MapGet("/permission-requests", async (string? from, string? to, ReportService service, HttpResponse response,
            CancellationToken cancellationToken) =>
        {
            response.Headers.CacheControl = "no-store";
            return (await service.PermissionRequests(from, to, cancellationToken)).ToHttp();
        });
        reports.MapGet("/workshop-delivery-times", async (string? from, string? to, ReportService service, HttpResponse response,
            CancellationToken cancellationToken) =>
        {
            response.Headers.CacheControl = "no-store";
            return (await service.WorkshopDeliveryTimes(from, to, cancellationToken)).ToHttp();
        });
    }
}
