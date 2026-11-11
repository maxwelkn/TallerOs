using System.Net;
using TallerOs.Application.Workshop;
using TallerOs.Infrastructure.Workshop;

namespace TallerOs.IntegrationTests;

public sealed class WeatherApiWeatherTests
{
    private const string Canary = "fictitious-weather-canary";
    private const string ValidPayload = """{"location":{"name":"Santo Domingo","country":"Dominican Republic"},"current":{"last_updated_epoch":1700000000,"precip_mm":0.4}}""";

    [Fact]
    public async Task ParsesDocumentedFieldsAndUtcInstant()
    {
        using var handler = new Handler(request =>
        {
            Assert.Equal("https", request.RequestUri!.Scheme);
            Assert.Equal("api.weatherapi.com", request.RequestUri.Host);
            Assert.Equal("/v1/current.json", request.RequestUri.AbsolutePath);
            Assert.Contains("q=Santo%20Domingo", request.RequestUri.Query);
            return Json(ValidPayload);
        });
        using var client = Client(handler);

        var result = await new WeatherApiWeather(client, new(Canary, "Santo Domingo")).GetCurrent(default);

        Assert.Equal("Santo Domingo, Dominican Republic", result.Location);
        Assert.Equal(0.4m, result.PrecipitationMm);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), result.ObservedUtc);
        Assert.Equal(TimeSpan.Zero, result.ObservedUtc.Offset);
    }

    [Fact]
    public async Task ParametersAreEncodedInsteadOfChangingQueryStructure()
    {
        using var handler = new Handler(request =>
        {
            Assert.Contains("key=test%2B%26%3F", request.RequestUri!.Query);
            Assert.Contains("q=City%20%26%20Area", request.RequestUri.Query);
            return Json(ValidPayload);
        });
        using var client = Client(handler);

        await new WeatherApiWeather(client, new("test+&?", "City & Area")).GetCurrent(default);
    }

    [Theory]
    [InlineData(null, "Santo Domingo")]
    [InlineData("", "Santo Domingo")]
    [InlineData("   ", "Santo Domingo")]
    [InlineData(Canary, null)]
    [InlineData(Canary, "")]
    public async Task MissingConfiguration_IsRejectedWithoutRequest(string? key, string? location)
    {
        var calls = 0;
        using var handler = new Handler(_ => { calls++; return Json(ValidPayload); });
        using var client = Client(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(() => new WeatherApiWeather(client, new(key, location)).GetCurrent(default));

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.MovedPermanently)]
    public async Task ProviderFailure_DoesNotExposeBodyOrCredentials(HttpStatusCode status)
    {
        using var handler = new Handler(_ => new(status) { Content = new StringContent(Canary + " internal provider body") });
        using var client = Client(handler);

        var error = await Assert.ThrowsAsync<WeatherUnavailableException>(() =>
            new WeatherApiWeather(client, new(Canary, "Santo Domingo")).GetCurrent(default));

        Assert.DoesNotContain(Canary, error.ToString());
        Assert.DoesNotContain("internal provider", error.Message);
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"location\":null}")]
    [InlineData("{\"location\":{\"name\":\"\",\"country\":\"X\"},\"current\":{\"last_updated_epoch\":1700000000,\"precip_mm\":0}}")]
    [InlineData("{\"location\":{\"name\":\"X\",\"country\":\"Y\"},\"current\":{\"last_updated_epoch\":0,\"precip_mm\":0}}")]
    [InlineData("{\"location\":{\"name\":\"X\",\"country\":\"Y\"},\"current\":{\"last_updated_epoch\":9223372036854775807,\"precip_mm\":0}}")]
    [InlineData("{\"location\":{\"name\":\"X\",\"country\":\"Y\"},\"current\":{\"last_updated_epoch\":1700000000,\"precip_mm\":-1}}")]
    [InlineData("{\"location\":{\"name\":\"X\",\"country\":\"Y\"},\"current\":{\"last_updated_epoch\":1700000000,\"precip_mm\":\"invalid\"}}")]
    public async Task InvalidPayload_IsControlledInsteadOfInventingData(string payload)
    {
        using var handler = new Handler(_ => Json(payload));
        using var client = Client(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(() =>
            new WeatherApiWeather(client, new(Canary, "Santo Domingo")).GetCurrent(default));
    }

    [Fact]
    public async Task NetworkFailureWithSecretInException_IsSanitized()
    {
        using var handler = new Handler(_ => throw new HttpRequestException("https://api.weatherapi.com/?key=" + Canary));
        using var client = Client(handler);

        var error = await Assert.ThrowsAsync<WeatherUnavailableException>(() =>
            new WeatherApiWeather(client, new(Canary, "Santo Domingo")).GetCurrent(default));

        Assert.DoesNotContain(Canary, error.ToString());
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task Timeout_IsControlled()
    {
        using var handler = new Handler(_ => throw new TaskCanceledException(Canary));
        using var client = Client(handler);

        var error = await Assert.ThrowsAsync<WeatherUnavailableException>(() =>
            new WeatherApiWeather(client, new(Canary, "Santo Domingo")).GetCurrent(default));

        Assert.DoesNotContain(Canary, error.ToString());
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated()
    {
        using var handler = new Handler(_ => Json(ValidPayload));
        using var client = Client(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new WeatherApiWeather(client, new(Canary, "Santo Domingo")).GetCurrent(cancellation.Token));
    }

    [Fact]
    public async Task OversizedResponse_IsControlled()
    {
        using var handler = new Handler(_ => Json(new string('x', 65537)));
        using var client = Client(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(() =>
            new WeatherApiWeather(client, new(Canary, "Santo Domingo")).GetCurrent(default));
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new("https://api.weatherapi.com/v1/"), MaxResponseContentBufferSize = 65536
    };

    private static HttpResponseMessage Json(string payload) => new(HttpStatusCode.OK) { Content = new StringContent(payload) };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
