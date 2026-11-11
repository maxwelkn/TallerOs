using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using TallerOs.Api.Http;
using TallerOs.Application.Common;
using TallerOs.Application.Documents;
using TallerOs.Domain.Documents;

namespace TallerOs.Api.Documents;

public static class DocumentEndpoints
{
    private const long MaximumRequestSize = Document.MaximumSize + 65536;

    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var documents = app.MapGroup("/api/documents");
        documents.MapPost("", Upload);
        documents.MapGet("", async (DocumentService service, CancellationToken cancellationToken) =>
            (await service.List(cancellationToken)).ToHttp());
        documents.MapGet("/{id:guid}/download", async (Guid id, HttpResponse response, DocumentService service,
            CancellationToken cancellationToken) =>
        {
            var outcome = await service.Download(id, cancellationToken);
            if (outcome.Failure != Failure.None || outcome.Value is not { } file) return outcome.ToHttp();
            response.Headers.XContentTypeOptions = "nosniff";
            response.Headers.CacheControl = "no-store";
            return Results.Stream(file.Content, "application/octet-stream", file.Name);
        });
        documents.MapDelete("/{id:guid}", async (Guid id, DocumentService service,
            CancellationToken cancellationToken) =>
        {
            var outcome = await service.Delete(id, cancellationToken);
            return outcome.Failure == Failure.None ? Results.NoContent() : outcome.ToHttp();
        });
    }

    private static async Task<IResult> Upload(HttpRequest request, DocumentService service,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumRequestSize)
            return TooLarge();
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var requestType)
            || !requestType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { message = "Envía un archivo multipart en el campo file." });

        var requestLimit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (requestLimit is { IsReadOnly: false }) requestLimit.MaxRequestBodySize = MaximumRequestSize;
        try
        {
            var form = await request.ReadFormAsync(new FormOptions
            {
                MultipartBodyLengthLimit = Document.MaximumSize, ValueCountLimit = 8,
                ValueLengthLimit = 1024, KeyLengthLimit = 128
            }, cancellationToken);
            if (form.Files.Count != 1 || form.Files[0].Name != "file")
                return Results.BadRequest(new { message = "Envía exactamente un archivo en el campo file." });

            var file = form.Files[0];
            if (!MediaTypeHeaderValue.TryParse(file.ContentType, out var contentType))
                return Results.BadRequest(new { message = "El tipo del documento no es válido." });
            var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
            await using var content = file.OpenReadStream();
            var outcome = await service.Upload(name, contentType.MediaType.Value, file.Length, content, cancellationToken);
            return outcome.Failure == Failure.None && outcome.Value is { } value
                ? Results.Created($"/api/documents/{value.Id}/download", value) : outcome.ToHttp();
        }
        catch (BadHttpRequestException error)
        {
            return error.StatusCode == StatusCodes.Status413PayloadTooLarge ? TooLarge()
                : Results.BadRequest(new { message = "No se pudo leer el archivo enviado." });
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest(new { message = "El formulario no es válido o supera el máximo de 10 MiB por archivo." });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            loggerFactory.CreateLogger(typeof(DocumentEndpoints).FullName ?? nameof(DocumentEndpoints))
                .LogWarning(error, "No se pudo procesar el archivo recibido.");
            return Results.Json(new { message = "No se pudo procesar el archivo. Intenta más tarde." }, statusCode: 503);
        }
    }

    private static IResult TooLarge() => Results.Json(
        new { message = "La petición supera el límite permitido; el archivo admite hasta 10 MiB." }, statusCode: 413);
}
