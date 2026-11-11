using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Application.Workshop;
using TallerOs.Domain.Workshop;

namespace TallerOs.UnitTests;

public sealed class DeliveryReviewTests
{
    [Theory]
    [InlineData(0, DeliveryWeatherAdvice.NoPrecipitationObserved)]
    [InlineData(0.1, DeliveryWeatherAdvice.CoveredHandoverRecommended)]
    [InlineData(20, DeliveryWeatherAdvice.CoveredHandoverRecommended)]
    public void Domain_InterpretsPrecipitation(decimal precipitation, DeliveryWeatherAdvice expected) =>
        Assert.Equal(expected, DeliveryWeatherAssessment.Evaluate(precipitation));

    [Fact]
    public void Domain_RejectsNegativePrecipitation() =>
        Assert.Throws<WorkshopRuleException>(() => DeliveryWeatherAssessment.Evaluate(-1));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RD_06_AuthorizedActorIsRequiredBeforeReadingWeather(bool missingActor)
    {
        var actor = new ActorStub(missingActor ? null : new(Guid.NewGuid(), false));
        var reader = new ReaderStub();
        var weather = new WeatherStub();
        var service = new DeliveryReviewService(new(actor, new AccessStub(false), reader), weather);

        var result = await service.Review(reader.Order.Id);

        Assert.Equal(missingActor ? Failure.Unauthorized : Failure.Forbidden, result.Failure);
        Assert.Equal(0, reader.Calls);
        Assert.Equal(0, weather.Calls);
    }

    [Theory]
    [InlineData("Received")]
    [InlineData("Diagnosed")]
    [InlineData("Authorized")]
    [InlineData("Delivered")]
    public async Task WrongOrderState_IsRejectedBeforeWeather(string state)
    {
        var reader = new ReaderStub(state);
        var weather = new WeatherStub();
        var service = Service(reader, weather);

        var result = await service.Review(reader.Order.Id);

        Assert.Equal(Failure.Conflict, result.Failure);
        Assert.Equal(0, weather.Calls);
        Assert.Equal(state, reader.Order.Status);
    }

    [Fact]
    public async Task MissingOrderOrEmptyId_DoesNotQueryWeather()
    {
        var reader = new ReaderStub();
        var weather = new WeatherStub();
        var service = Service(reader, weather);

        Assert.Equal(Failure.NotFound, (await service.Review(Guid.NewGuid())).Failure);
        Assert.Equal(Failure.Invalid, (await service.Review(Guid.Empty)).Failure);
        Assert.Equal(0, weather.Calls);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public async Task AuthorizedReview_CombinesOrderAndWeatherWithoutMutatingOrder(bool administrator, decimal precipitation)
    {
        var reader = new ReaderStub();
        var weather = new WeatherStub { Precipitation = precipitation };
        var service = Service(reader, weather, administrator);

        var result = await service.Review(reader.Order.Id);

        Assert.Equal(Failure.None, result.Failure);
        Assert.NotNull(result.Value);
        Assert.Equal(reader.Order.Id, result.Value.OrderId);
        Assert.Equal("InRepair", result.Value.OrderStatus);
        Assert.Equal("Taller de prueba", result.Value.Location);
        Assert.Equal(weather.ObservedUtc, result.Value.ObservedUtc);
        Assert.Equal(precipitation, result.Value.PrecipitationMm);
        Assert.Contains(precipitation > 0 ? "bajo techo" : "No se observa precipitación", result.Value.Recommendation);
        Assert.Equal("InRepair", reader.Order.Status);
        Assert.Equal(1, weather.Calls);
    }

    [Fact]
    public async Task RD_08_WeatherFailure_IsControlledAndOrderIsUnchanged()
    {
        var reader = new ReaderStub();
        var weather = new WeatherStub { Unavailable = true };

        var result = await Service(reader, weather).Review(reader.Order.Id);

        Assert.Equal(Failure.Unavailable, result.Failure);
        Assert.Null(result.Value);
        Assert.Equal("InRepair", reader.Order.Status);
    }

    [Fact]
    public async Task InvalidPrecipitationFromPort_DoesNotProduceMisleadingAdvice()
    {
        var reader = new ReaderStub();
        var weather = new WeatherStub { Precipitation = -1 };

        Assert.Equal(Failure.Unavailable, (await Service(reader, weather).Review(reader.Order.Id)).Failure);
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated()
    {
        var reader = new ReaderStub();
        var weather = new WeatherStub();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Service(reader, weather).Review(reader.Order.Id, cancellation.Token));
    }

    private static DeliveryReviewService Service(ReaderStub reader, WeatherStub weather, bool administrator = false) =>
        new(new(new ActorStub(new(Guid.NewGuid(), administrator)), new AccessStub(!administrator), reader), weather);

    private sealed class ActorStub(Actor? actor) : ICurrentActor
    {
        public Actor? Get() => actor;
    }

    private sealed class AccessStub(bool granted) : IAccessPermissions
    {
        public Task<bool> HasPermission(Guid userId, string permission, CancellationToken cancellationToken) => Task.FromResult(granted);
        public Task StageGrant(Guid userId, string permission, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ReaderStub(string state = "InRepair") : IWorkOrderReader
    {
        public WorkOrderDetailsView Order { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "Prueba", state, []);
        public int Calls { get; private set; }
        public Task<IReadOnlyList<WorkOrderView>> List(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkOrderView>>([]);

        public Task<WorkOrderDetailsView?> Find(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(id == Order.Id ? Order : null);
        }
    }

    private sealed class WeatherStub : IWorkshopWeather
    {
        public decimal Precipitation { get; init; }
        public bool Unavailable { get; init; }
        public int Calls { get; private set; }
        public DateTimeOffset ObservedUtc { get; } = DateTimeOffset.FromUnixTimeSeconds(1700000000);

        public Task<WeatherObservation> GetCurrent(CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            if (Unavailable) throw new WeatherUnavailableException("No se pudo consultar el clima.");
            return Task.FromResult(new WeatherObservation("Taller de prueba", ObservedUtc, Precipitation));
        }
    }
}
