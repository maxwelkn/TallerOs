using TallerOs.Api.Http;
using TallerOs.Application.Mail;

namespace TallerOs.Api.Mail;

public static class MailOutboxEndpoints
{
    public static void MapMailOutboxEndpoints(this WebApplication app) =>
        app.MapGet("/api/admin/mail-outbox", async (HttpResponse response, MailOutboxService service,
            CancellationToken cancellationToken) =>
        {
            response.Headers.CacheControl = "no-store";
            return (await service.List(cancellationToken)).ToHttp();
        });
}
