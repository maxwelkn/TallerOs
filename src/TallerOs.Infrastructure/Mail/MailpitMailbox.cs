using System.Net;
using System.Text.Json;
using TallerOs.Application.Mail;

namespace TallerOs.Infrastructure.Mail;

public sealed class MailpitMailbox(HttpClient client) : IDemoMailbox
{
    public async Task<DemoMailPage> List(int start, CancellationToken cancellationToken) =>
        await Read(async () =>
        {
            using var response = await client.GetAsync($"api/v1/messages?start={start}&limit=50", cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var messages = root.GetProperty("messages").EnumerateArray().Select(message =>
                new DemoMailSummary(Identity(message), RequiredText(message, "Subject"), Recipients(message))).ToArray();
            return new DemoMailPage(root.GetProperty("total").GetInt32(), start, 50, messages);
        }, cancellationToken);

    public async Task<DemoMailMessage?> Find(Guid id, CancellationToken cancellationToken) =>
        await Read<DemoMailMessage?>(async () =>
        {
            using var response = await client.GetAsync($"api/v1/message/{id}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var messageId = Identity(root);
            if (messageId != id) throw new JsonException("Identificador inesperado.");
            return new(messageId, RequiredText(root, "Subject"), Recipients(root), RequiredText(root, "Text"));
        }, cancellationToken);

    private static Guid Identity(JsonElement message) => Guid.Parse(RequiredText(message, "ID"));

    private static string RequiredText(JsonElement message, string property) =>
        message.GetProperty(property).GetString() ?? throw new JsonException("Campo ausente.");

    private static string[] Recipients(JsonElement message) => message.GetProperty("To").EnumerateArray()
        .Select(address => RequiredText(address, "Address")).ToArray();

    private static async Task<T> Read<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        try { return await operation(); }
        catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DemoMailboxException("Tiempo de espera agotado.", error);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException
            or KeyNotFoundException or FormatException or InvalidOperationException)
        {
            throw new DemoMailboxException("No se pudo consultar el buzón.", error);
        }
    }
}
