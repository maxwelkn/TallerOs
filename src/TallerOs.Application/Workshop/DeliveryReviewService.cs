using TallerOs.Application.Common;
using TallerOs.Domain.Workshop;

namespace TallerOs.Application.Workshop;

public sealed record WeatherObservation(string Location, DateTimeOffset ObservedUtc, decimal PrecipitationMm);
public sealed record DeliveryReviewView(Guid OrderId, string OrderStatus, string Location,
    DateTimeOffset ObservedUtc, decimal PrecipitationMm, string Recommendation);
public sealed class WeatherUnavailableException(string message) : Exception(message);

public interface IWorkshopWeather
{
    Task<WeatherObservation> GetCurrent(CancellationToken cancellationToken);
}

public sealed class DeliveryReviewService(WorkOrderQuery orders, IWorkshopWeather weather)
{
    public async Task<Outcome<DeliveryReviewView>> Review(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await orders.Find(id, cancellationToken);
        if (result.Failure != Failure.None)
            return Outcome<DeliveryReviewView>.Fail(result.Failure, result.Message ?? "No se pudo consultar la orden.");
        if (result.Value is not { } order)
            return Outcome<DeliveryReviewView>.Fail(Failure.Unavailable, "No se pudo consultar la orden.");
        if (order.Status != nameof(WorkOrderStatus.InRepair))
            return Outcome<DeliveryReviewView>.Fail(Failure.Conflict,
                "La revisión de entrega requiere una orden en reparación (InRepair).");
        try
        {
            var observation = await weather.GetCurrent(cancellationToken);
            var advice = DeliveryWeatherAssessment.Evaluate(observation.PrecipitationMm);
            var recommendation = advice == DeliveryWeatherAdvice.CoveredHandoverRecommended
                ? "Se observa precipitación. Coordina la entrega bajo techo."
                : "No se observa precipitación en el taller al momento de la observación.";
            return new(new(order.Id, order.Status, observation.Location, observation.ObservedUtc,
                observation.PrecipitationMm, recommendation));
        }
        catch (WeatherUnavailableException error)
        {
            return Outcome<DeliveryReviewView>.Fail(Failure.Unavailable, error.Message);
        }
        catch (WorkshopRuleException)
        {
            return Outcome<DeliveryReviewView>.Fail(Failure.Unavailable, "La observación meteorológica no es válida.");
        }
    }
}
