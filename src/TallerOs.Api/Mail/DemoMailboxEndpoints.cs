using TallerOs.Api.Http;
using TallerOs.Application.Mail;

namespace TallerOs.Api.Mail;

public static class DemoMailboxEndpoints
{
    public static void MapDemoMailboxEndpoints(this WebApplication app)
    {
        var mailbox = app.MapGroup("/api/admin/demo-mail");
        mailbox.MapGet("", async (int? start, HttpResponse response, DemoMailboxService service,
            CancellationToken cancellationToken) =>
        {
            response.Headers.CacheControl = "no-store";
            return (await service.List(start ?? 0, cancellationToken)).ToHttp();
        });
        mailbox.MapGet("/{id}", async (string id, HttpResponse response, DemoMailboxService service,
            CancellationToken cancellationToken) =>
        {
            response.Headers.CacheControl = "no-store";
            return (await service.Find(id, cancellationToken)).ToHttp();
        });
    }
}
