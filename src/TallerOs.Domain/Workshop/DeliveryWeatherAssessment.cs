namespace TallerOs.Domain.Workshop;

public enum DeliveryWeatherAdvice { NoPrecipitationObserved, CoveredHandoverRecommended }

public static class DeliveryWeatherAssessment
{
    public static DeliveryWeatherAdvice Evaluate(decimal precipitationMm)
    {
        if (precipitationMm < 0)
            throw new WorkshopRuleException("La precipitación observada no puede ser negativa.");
        return precipitationMm > 0 ? DeliveryWeatherAdvice.CoveredHandoverRecommended
            : DeliveryWeatherAdvice.NoPrecipitationObserved;
    }
}
