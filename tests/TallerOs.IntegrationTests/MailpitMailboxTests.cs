using System.Net;
using TallerOs.Application.Mail;
using TallerOs.Infrastructure.Mail;

namespace TallerOs.IntegrationTests;

public sealed class MailpitMailboxTests
{
    private const string MessageId = "abcdefghijklmnopqrstuv";

    [Fact]
    public async Task List_TranslatesMailpitSchemaAndPagination()
    {
        using var handler = new Handler(request =>
        {
            Assert.Equal("/api/v1/messages?start=50&limit=50", request.RequestUri!.PathAndQuery);
            return Json("""{"total":51,"messages":[{"ID":"abcdefghijklmnopqrstuv","Subject":"Activación","To":[{"Address":"a@example.test"}]}]}""");
        });
        using var client = Client(handler);

        var result = await new MailpitMailbox(client).List(50, default);

        Assert.Equal(51, result.Total);
        Assert.Equal(MessageId, Assert.Single(result.Messages).Id);
        Assert.Equal("a@example.test", Assert.Single(result.Messages[0].To));
    }

    [Fact]
    public async Task Find_ReturnsOnlyTextAndExpectedMessage()
    {
        using var handler = new Handler(_ => Json("""{"ID":"abcdefghijklmnopqrstuv","Subject":"Activación","To":[{"Address":"a@example.test"}],"Text":"Enlace ficticio","HTML":"<script>unsafe</script>"}"""));
        using var client = Client(handler);

        var result = await new MailpitMailbox(client).Find(MessageId, default);

        Assert.NotNull(result);
        Assert.Equal("Enlace ficticio", result.Text);
        Assert.Equal(MessageId, result.Id);
    }

    [Fact]
    public async Task Find_MissingMessageReturnsNull()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.NotFound));
        using var client = Client(handler);

        Assert.Null(await new MailpitMailbox(client).Find(MessageId, default));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"messages\":null,\"total\":0}")]
    [InlineData("{\"messages\":[{\"ID\":\"invalid\"}],\"total\":1}")]
    public async Task InvalidUpstreamPayload_IsWrapped(string payload)
    {
        using var handler = new Handler(_ => Json(payload));
        using var client = Client(handler);

        await Assert.ThrowsAsync<DemoMailboxException>(() => new MailpitMailbox(client).List(0, default));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task UpstreamFailure_IsWrapped(HttpStatusCode status)
    {
        using var handler = new Handler(_ => new(status));
        using var client = Client(handler);

        await Assert.ThrowsAsync<DemoMailboxException>(() => new MailpitMailbox(client).List(0, default));
    }

    [Fact]
    public async Task Timeout_IsWrapped()
    {
        using var handler = new Handler(_ => throw new TaskCanceledException("Simulated timeout"));
        using var client = Client(handler);

        await Assert.ThrowsAsync<DemoMailboxException>(() => new MailpitMailbox(client).List(0, default));
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated()
    {
        using var handler = new Handler(_ => throw new OperationCanceledException());
        using var client = Client(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MailpitMailbox(client).List(0, cancellation.Token));
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new("http://mailpit/") };
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
