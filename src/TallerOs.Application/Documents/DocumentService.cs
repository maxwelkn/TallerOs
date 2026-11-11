using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Domain.Documents;

namespace TallerOs.Application.Documents;

public sealed class DocumentService(IDocumentStore store, IDocumentFiles files,
    ICurrentActor currentActor, TimeProvider clock)
{
    public Task<Outcome<DocumentView>> Upload(string? name, string? contentType, long size, Stream content,
        CancellationToken cancellationToken = default) => Execute(async actor =>
    {
        var document = Document.Create(name, contentType, size, actor.Id, clock.GetUtcNow());
        if (!content.CanRead)
            return Outcome<DocumentView>.Fail(Failure.Invalid, "No se puede leer el archivo.");

        await files.Write(document.StorageKey, content, document.Size, cancellationToken);
        try
        {
            store.Add(document);
            await store.Save(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await files.RemoveNew(document.StorageKey, CancellationToken.None);
            throw;
        }
        catch (DocumentStorageException)
        {
            if (!await files.RemoveNew(document.StorageKey, CancellationToken.None))
                return Outcome<DocumentView>.Fail(Failure.Unavailable,
                    "No se pudo registrar el documento ni retirar su archivo nuevo. El almacenamiento requiere revisión.");
            throw;
        }
        return new Outcome<DocumentView>(DocumentView.From(document));
    });

    public Task<Outcome<IReadOnlyList<DocumentView>>> List(CancellationToken cancellationToken = default) =>
        Execute<IReadOnlyList<DocumentView>>(async actor =>
        {
            var documents = await store.ListVisible(actor.Id, actor.IsAdministrator, cancellationToken);
            return new(documents.Select(DocumentView.From).ToArray());
        });

    public Task<Outcome<DocumentDownload>> Download(Guid id, CancellationToken cancellationToken = default) =>
        Execute(async actor =>
        {
            var document = await store.Find(id, cancellationToken);
            if (document is null || document.IsDeleted)
                return Outcome<DocumentDownload>.Fail(Failure.NotFound, "Documento no encontrado.");
            if (!document.CanAccess(actor.Id, actor.IsAdministrator))
                return Outcome<DocumentDownload>.Fail(Failure.Forbidden, "No tienes acceso a este documento.");

            var content = await files.OpenRead(document.StorageKey, cancellationToken);
            return content is null
                ? Outcome<DocumentDownload>.Fail(Failure.Unavailable, "El contenido del documento no está disponible.")
                : new Outcome<DocumentDownload>(new(content, document.Name, document.ContentType));
        });

    public Task<Outcome<DocumentView>> Delete(Guid id, CancellationToken cancellationToken = default) =>
        Execute(async actor =>
        {
            var document = await store.Find(id, cancellationToken);
            if (document is null || document.IsDeleted)
                return Outcome<DocumentView>.Fail(Failure.NotFound, "Documento no encontrado.");
            if (!document.CanAccess(actor.Id, actor.IsAdministrator))
                return Outcome<DocumentView>.Fail(Failure.Forbidden, "No tienes acceso a este documento.");

            document.Delete(actor.Id, actor.IsAdministrator);
            await store.Save(cancellationToken);
            return new Outcome<DocumentView>(DocumentView.From(document));
        });

    private async Task<Outcome<T>> Execute<T>(Func<Actor, Task<Outcome<T>>> operation)
    {
        var actor = currentActor.Get();
        if (actor is null || actor.Id == Guid.Empty)
            return Outcome<T>.Fail(Failure.Unauthorized, "Inicia sesión.");
        try { return await operation(actor); }
        catch (DocumentRuleException error)
        {
            return Outcome<T>.Fail(Failure.Invalid, error.Message);
        }
        catch (DocumentStorageException)
        {
            return Outcome<T>.Fail(Failure.Unavailable, "No se pudo acceder al almacenamiento de documentos. Intenta más tarde.");
        }
    }
}
