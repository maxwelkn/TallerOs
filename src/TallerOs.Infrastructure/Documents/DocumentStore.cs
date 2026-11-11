using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TallerOs.Application.Documents;
using TallerOs.Domain.Documents;

namespace TallerOs.Infrastructure.Documents;

public sealed class DocumentStore(DocumentDbContext db, ILogger<DocumentStore> logger) : IDocumentStore
{
    public async Task<IReadOnlyList<Document>> ListVisible(Guid actorId, bool isAdministrator,
        CancellationToken cancellationToken) => await Access(async () =>
            (IReadOnlyList<Document>)await db.Documents.AsNoTracking()
                .Where(item => !item.IsDeleted && (isAdministrator || item.UploadedBy == actorId))
                .OrderByDescending(item => item.UploadedUtc).ThenBy(item => item.Id).ToListAsync(cancellationToken));

    public Task<Document?> Find(Guid id, CancellationToken cancellationToken) =>
        Access(() => db.Documents.SingleOrDefaultAsync(item => item.Id == id, cancellationToken));

    public void Add(Document document) => db.Documents.Add(document);

    public async Task Save(CancellationToken cancellationToken) =>
        await Access(() => db.SaveChangesAsync(cancellationToken));

    private async Task<T> Access<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (Exception error) when (error is DbUpdateException or SqliteException)
        {
            logger.LogWarning(error, "Falló el almacenamiento de metadatos de documentos.");
            throw new DocumentStorageException("No se pudo acceder a los metadatos.", error);
        }
    }
}
