using TallerOs.Application.Notifications;

namespace TallerOs.UnitTests;

internal sealed class FakeNotifications : INotificationPublisher
{
    public Task StageForAdministrators(string eventKey, string subject, string message, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task StageForUser(Guid userId, string eventKey, string subject, string message, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
