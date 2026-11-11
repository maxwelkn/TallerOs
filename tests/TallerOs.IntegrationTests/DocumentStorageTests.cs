using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Application.Documents;
using TallerOs.Domain.Documents;
using TallerOs.Infrastructure.Documents;

namespace TallerOs.IntegrationTests;

public sealed class DocumentStorageTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("talleros-documents-");
    private readonly Guid owner = Guid.NewGuid();
    private string FilesPath => Path.Combine(directory.FullName, "files");
    private LocalDocumentFiles Files => new(FilesPath, NullLogger<LocalDocumentFiles>.Instance);

    [Fact]
    public async Task RF_DOC_01_RD_09_MigrationAndNewContext_PreserveMetadataAndBytes()
    {
        var bytes = new byte[] { 0, 255, 42, 9 };
        DocumentView saved;
        await using (var db = Context())
        {
            await db.Database.MigrateAsync();
            var result = await Service(db).Upload("manual.bin", "application/octet-stream", bytes.Length, new MemoryStream(bytes));
            Assert.Equal(Failure.None, result.Failure);
            saved = Assert.IsType<DocumentView>(result.Value);
        }

        await using var reopened = Context();
        await reopened.Database.MigrateAsync();
        var service = Service(reopened);
        var listed = await service.List();
        var download = await service.Download(saved.Id);
        await using var content = Assert.IsType<DocumentDownload>(download.Value).Content;
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);

        Assert.Equal(saved, Assert.Single(listed.Value!));
        Assert.Equal(bytes, buffer.ToArray());
        Assert.Empty(await reopened.Database.GetPendingMigrationsAsync());
        await using var command = reopened.Database.GetDbConnection().CreateCommand();
        await reopened.Database.OpenConnectionAsync();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users'";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task RF_DOC_02_RealStore_FiltersOwnersAndDeletedForBothRoles()
    {
        await using var db = Context();
        await db.Database.MigrateAsync();
        var own = Document.Create("own.txt", "text/plain", 1, owner, DateTimeOffset.UtcNow.AddMinutes(-1));
        var other = Document.Create("other.txt", "text/plain", 1, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var deleted = Document.Create("deleted.txt", "text/plain", 1, owner, DateTimeOffset.UtcNow);
        deleted.Delete(owner, false);
        db.AddRange(own, other, deleted);
        await db.SaveChangesAsync();
        var store = new DocumentStore(db, NullLogger<DocumentStore>.Instance);

        var visible = await store.ListVisible(owner, false, default);
        var administrative = await store.ListVisible(Guid.NewGuid(), true, default);

        Assert.Equal(own.Id, Assert.Single(visible).Id);
        Assert.Equal(new[] { other.Id, own.Id }, administrative.Select(item => item.Id));
    }

    [Fact]
    public async Task RF_DOC_04_LogicalDeletion_PreservesRowAndPhysicalContent()
    {
        Guid id;
        string key;
        await using (var db = Context())
        {
            await db.Database.MigrateAsync();
            var service = Service(db);
            var uploaded = await service.Upload("a.txt", "text/plain", 1, new MemoryStream([42]));
            id = uploaded.Value!.Id;
            key = (await db.Documents.SingleAsync()).StorageKey;
            Assert.Equal(Failure.None, (await service.Delete(id)).Failure);
        }

        await using var reopened = Context();
        var row = await reopened.Documents.SingleAsync();
        var administrative = Service(reopened, administrator: true);

        Assert.True(row.IsDeleted);
        Assert.Equal(id, row.Id);
        Assert.Equal(new byte[] { 42 }, await File.ReadAllBytesAsync(Path.Combine(FilesPath, key)));
        Assert.Empty((await administrative.List()).Value!);
        Assert.Equal(Failure.NotFound, (await administrative.Download(id)).Failure);
        Assert.Equal(Failure.NotFound, (await administrative.Delete(id)).Failure);
    }

    [Fact]
    public async Task RF_DOC_01_RealDatabaseFailure_RemovesOnlyUnregisteredFile()
    {
        await using var db = Context();
        var existingKey = Guid.NewGuid().ToString("N");
        await Files.Write(existingKey, new MemoryStream([7]), 1, default);

        var result = await Service(db).Upload("a.txt", "text/plain", 1, new MemoryStream([42]));

        Assert.Equal(Failure.Unavailable, result.Failure);
        Assert.Equal(existingKey, Path.GetFileName(Assert.Single(Directory.GetFiles(FilesPath))));
        Assert.Equal(new byte[] { 7 }, await File.ReadAllBytesAsync(Path.Combine(FilesPath, existingKey)));
        Assert.DoesNotContain(directory.FullName, result.Message!);
    }

    [Fact]
    public async Task RD_08_MissingRealFile_ProducesControlledFailure()
    {
        await using var db = Context();
        await db.Database.MigrateAsync();
        var service = Service(db);
        var uploaded = await service.Upload("a.txt", "text/plain", 1, new MemoryStream([42]));
        var document = await db.Documents.SingleAsync();
        File.Delete(Path.Combine(FilesPath, document.StorageKey));

        var result = await service.Download(uploaded.Value!.Id);

        Assert.Equal(Failure.Unavailable, result.Failure);
        Assert.NotNull(await db.Documents.FindAsync(document.Id));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    [InlineData(Document.MaximumSize + 1, Document.MaximumSize)]
    public async Task RD_07_ActualSizeMismatch_RejectsAndCleansPartialFile(int actual, long expected)
    {
        var key = Guid.NewGuid().ToString("N");

        await Assert.ThrowsAsync<DocumentRuleException>(() =>
            Files.Write(key, new MemoryStream(new byte[actual]), expected, default));

        Assert.Empty(Directory.GetFiles(FilesPath));
    }

    [Fact]
    public async Task RD_07_MaximumSize_CanBeStoredWithoutTruncation()
    {
        var key = Guid.NewGuid().ToString("N");
        var bytes = new byte[Document.MaximumSize];
        bytes[^1] = 42;

        await Files.Write(key, new MemoryStream(bytes), bytes.Length, default);

        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(FilesPath, key)));
    }

    [Fact]
    public async Task RD_08_InterruptedStream_RemovesPartialFile()
    {
        var key = Guid.NewGuid().ToString("N");

        await Assert.ThrowsAsync<DocumentStorageException>(() => Files.Write(key, new InterruptedStream(), 2, default));

        Assert.Empty(Directory.GetFiles(FilesPath));
    }

    [Fact]
    public async Task RD_08_Collision_DoesNotOverwriteOrDeleteExistingContent()
    {
        var key = Guid.NewGuid().ToString("N");
        await Files.Write(key, new MemoryStream([7]), 1, default);

        await Assert.ThrowsAsync<DocumentStorageException>(() => Files.Write(key, new MemoryStream([42]), 1, default));

        Assert.Equal(new byte[] { 7 }, await File.ReadAllBytesAsync(Path.Combine(FilesPath, key)));
    }

    [Fact]
    public async Task RD_08_UnwritableDirectory_RejectsWithoutSavingMetadata()
    {
        await using var db = Context();
        await db.Database.MigrateAsync();
        await File.WriteAllTextAsync(FilesPath, "not a directory");

        var result = await Service(db).Upload("a.txt", "text/plain", 1, new MemoryStream([42]));

        Assert.Equal(Failure.Unavailable, result.Failure);
        Assert.Empty(await db.Documents.ToArrayAsync());
    }

    [Fact]
    public async Task RD_06_StorageKeyCannotTraverseDirectories()
    {
        await Assert.ThrowsAsync<DocumentStorageException>(() =>
            Files.Write("../outside", new MemoryStream([42]), 1, default));
        Assert.False(Directory.Exists(FilesPath));
    }

    [Fact]
    public async Task RD_07_CanceledWrite_RemovesPartialFileAndPropagates()
    {
        var key = Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Files.Write(key, new MemoryStream([42]), 1, cancellation.Token));

        Assert.Empty(Directory.GetFiles(FilesPath));
    }

    private DocumentDbContext Context() => new(new DbContextOptionsBuilder<DocumentDbContext>()
        .UseSqlite(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory.FullName, "documents.db"), Pooling = false
        }.ToString()).Options);

    private DocumentService Service(DocumentDbContext db, bool administrator = false) =>
        new(new DocumentStore(db, NullLogger<DocumentStore>.Instance), Files,
            new CurrentActor(new(owner, administrator)), TimeProvider.System);

    public void Dispose() => directory.Delete(recursive: true);

    private sealed class CurrentActor(Actor actor) : ICurrentActor
    {
        public Actor? Get() => actor;
    }

    private sealed class InterruptedStream() : MemoryStream([42])
    {
        private bool read;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (read) throw new IOException("Fallo simulado de lectura.");
            read = true;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
