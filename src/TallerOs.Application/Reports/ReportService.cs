using System.Globalization;
using TallerOs.Application.Access;
using TallerOs.Application.Common;

namespace TallerOs.Application.Reports;

public readonly record struct ReportDateRange(DateOnly From, DateOnly To)
{
    public static bool TryParse(string? from, string? to, out ReportDateRange range)
    {
        range = default;
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
            || end < start)
            return false;

        range = new(start, end);
        return true;
    }

    public bool Includes(DateTimeOffset instant)
    {
        var day = DateOnly.FromDateTime(instant.UtcDateTime);
        return day >= From && day <= To;
    }
}

public sealed record PermissionRequestSample(DateTimeOffset CreatedUtc, string Status);
public sealed record DeliveryTimeSample(DateTimeOffset DeliveredUtc, DateTimeOffset ReceivedUtc, string VehicleType);
public sealed record PermissionRequestTotal(string Month, string Status, int Count);
public sealed record WorkshopDeliveryTime(string VehicleType, int Count, double AverageHours);

public interface IPermissionReportSource
{
    Task<IReadOnlyList<PermissionRequestSample>> Read(Guid? requesterId, CancellationToken cancellationToken);
}

public interface IWorkshopReportSource
{
    Task<IReadOnlyList<DeliveryTimeSample>> Read(Guid? receivedBy, CancellationToken cancellationToken);
}

public sealed class ReportService(ICurrentActor currentActor, IPermissionReportSource permissions, IWorkshopReportSource workshop)
{
    public Task<Outcome<IReadOnlyList<PermissionRequestTotal>>> PermissionRequests(string? from, string? to,
        CancellationToken cancellationToken) =>
        Read<PermissionRequestTotal>(from, to, async (actorId, range) =>
        {
            var rows = await permissions.Read(actorId, cancellationToken);
            return rows.Where(row => range.Includes(row.CreatedUtc))
                .GroupBy(row => (Month: row.CreatedUtc.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture), row.Status))
                .OrderBy(group => group.Key.Month, StringComparer.Ordinal)
                .ThenBy(group => group.Key.Status, StringComparer.Ordinal)
                .Select(group => new PermissionRequestTotal(group.Key.Month, group.Key.Status, group.Count()))
                .ToArray();
        });

    public Task<Outcome<IReadOnlyList<WorkshopDeliveryTime>>> WorkshopDeliveryTimes(string? from, string? to,
        CancellationToken cancellationToken) =>
        Read<WorkshopDeliveryTime>(from, to, async (actorId, range) =>
        {
            var rows = await workshop.Read(actorId, cancellationToken);
            return rows.Where(row => range.Includes(row.DeliveredUtc) && row.DeliveredUtc >= row.ReceivedUtc)
                .GroupBy(row => row.VehicleType)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new WorkshopDeliveryTime(group.Key, group.Count(),
                    group.Average(row => (row.DeliveredUtc - row.ReceivedUtc).TotalHours)))
                .ToArray();
        });

    private async Task<Outcome<IReadOnlyList<T>>> Read<T>(string? from, string? to,
        Func<Guid?, ReportDateRange, Task<IReadOnlyList<T>>> read)
    {
        var actor = currentActor.Get();
        if (actor is null) return Outcome<IReadOnlyList<T>>.Fail(Failure.Unauthorized, "Inicia sesión.");
        if (!ReportDateRange.TryParse(from, to, out var range))
            return Outcome<IReadOnlyList<T>>.Fail(Failure.Invalid, "Indica un rango válido con from y to en formato YYYY-MM-DD.");

        return new(await read(actor.IsAdministrator ? null : actor.Id, range));
    }
}
