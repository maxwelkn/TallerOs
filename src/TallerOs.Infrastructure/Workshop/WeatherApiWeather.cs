using System.Net;
using System.Text.Json;
using TallerOs.Application.Workshop;

namespace TallerOs.Infrastructure.Workshop;

public sealed class WeatherApiSettings(string? apiKey, string? location)
{
    public string? ApiKey { get; } = apiKey?.Trim();
    public string? Location { get; } = location?.Trim();
}

public sealed class WeatherApiWeather(HttpClient client, WeatherApiSettings settings) : IWorkshopWeather
{
    public async Task<WeatherObservation> GetCurrent(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new WeatherUnavailableException("Configura WEATHER_API_KEY con una clave válida para consultar el clima.");
        if (string.IsNullOrWhiteSpace(settings.Location))
            throw new WeatherUnavailableException("Configura WORKSHOP_LOCATION para consultar el clima del taller.");

        try
        {
            var path = $"current.json?key={Uri.EscapeDataString(settings.ApiKey)}&q={Uri.EscapeDataString(settings.Location)}&aqi=no";
            using var response = await client.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new WeatherUnavailableException("El proveedor rechazó la clave de clima. Revisa WEATHER_API_KEY.");
            if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
                throw new WeatherUnavailableException("El proveedor no permite consultar el clima. Revisa la clave, el plan o su cuota.");
            if (!response.IsSuccessStatusCode)
                throw new WeatherUnavailableException("El servicio de clima no está disponible. Intenta más tarde.");

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var location = document.RootElement.GetProperty("location");
            var name = location.GetProperty("name").GetString();
            var country = location.GetProperty("country").GetString();
            var current = document.RootElement.GetProperty("current");
            var epoch = current.GetProperty("last_updated_epoch").GetInt64();
            var precipitation = current.GetProperty("precip_mm").GetDecimal();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(country) || epoch <= 0 || precipitation < 0)
                throw new JsonException("Observación no válida.");

            return new($"{name}, {country}", DateTimeOffset.FromUnixTimeSeconds(epoch), precipitation);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WeatherUnavailableException("El servicio de clima tardó demasiado. Intenta más tarde.");
        }
        catch (Exception error) when (error is HttpRequestException or IOException)
        {
            throw new WeatherUnavailableException("No se pudo contactar el servicio de clima. Intenta más tarde.");
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException
            or FormatException or OverflowException or ArgumentOutOfRangeException)
        {
            throw new WeatherUnavailableException("El servicio de clima devolvió datos no válidos. Intenta más tarde.");
        }
    }
}
