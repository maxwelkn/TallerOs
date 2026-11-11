using TallerOs.Domain.Documents;

namespace TallerOs.Application.Documents;

public sealed record DocumentView(Guid Id, string Name, string ContentType, long Size,
    DateTimeOffset UploadedUtc, Guid UploadedBy)
{
    public static DocumentView From(Document document) => new(document.Id, document.Name,
        document.ContentType, document.Size, document.UploadedUtc, document.UploadedBy);
}

public sealed record DocumentDownload(Stream Content, string Name, string ContentType);

public sealed class DocumentStorageException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public interface IDocumentStore
{
    Task<IReadOnlyList<Document>> ListVisible(Guid actorId, bool isAdministrator, CancellationToken cancellationToken);
    Task<Document?> Find(Guid id, CancellationToken cancellationToken);
    void Add(Document document);
    Task Save(CancellationToken cancellationToken);
}

public interface IDocumentFiles
{
    Task Write(string storageKey, Stream content, long expectedSize, CancellationToken cancellationToken);
    Task<Stream?> OpenRead(string storageKey, CancellationToken cancellationToken);
    Task<bool> RemoveNew(string storageKey, CancellationToken cancellationToken);
}
