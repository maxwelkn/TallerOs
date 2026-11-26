using TallerOs.Api.Http;
using TallerOs.Application.Notifications;

namespace TallerOs.Api.Notifications;

public sealed record SetNotificationEmailRequest(bool? Enabled);

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var notifications = app.MapGroup("/api/notifications");
        notifications.MapGet("", async (NotificationService service, CancellationToken cancellationToken) =>
            (await service.List(cancellationToken)).ToHttp());
        notifications.MapPut("/{id:guid}/read", async (Guid id, NotificationService service, CancellationToken cancellationToken) =>
            (await service.MarkRead(id, cancellationToken)).ToHttp());
        notifications.MapPut("/preferences/email", async (SetNotificationEmailRequest request, NotificationService service,
            CancellationToken cancellationToken) => (await service.SetEmail(request.Enabled, cancellationToken)).ToHttp());
    }
}
