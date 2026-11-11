using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Application.Documents;
using TallerOs.Domain.Documents;

namespace TallerOs.UnitTests;

public sealed class DocumentServiceTests
{
    [Theory]
    [InlineData("upload")]
    [InlineData("list")]
    [InlineData("download")]
    [InlineData("delete")]
    public async Task RD_06_WithoutActor_RejectsEveryOperation(string operation)
    {
        var scenario = new Scenario(null);

        var failure = await Run(scenario, operation);

        Assert.Equal(Failure.Unauthorized, failure);
        Assert.Equal(0, scenario.Store.ReadCount);
        Assert.Empty(scenario.Files.Keys);
    }

    [Fact]
    public async Task RF_DOC_01_Upload_UsesActorAndClock()
    {
        var scenario = new Scenario();

        var result = await scenario.Service.Upload("a.txt", "text/plain", 1, new MemoryStream([42]));

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(scenario.Actor.Value!.Id, result.Value!.UploadedBy);
        Assert.Equal(FixedClock.Instant, result.Value.UploadedUtc);
        Assert.Equal(1, scenario.Store.SaveCount);
        Assert.Contains(Assert.Single(scenario.Store.Documents).StorageKey, scenario.Files.Keys);
    }

    [Fact]
    public async Task RD_07_InvalidMetadata_DoesNotWriteOrSave()
    {
        var scenario = new Scenario();

        var result = await scenario.Service.Upload("a.txt", "text/plain", 0, new MemoryStream());

        Assert.Equal(Failure.Invalid, result.Failure);
        Assert.Empty(scenario.Files.Keys);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RF_DOC_01_FileFailure_DoesNotSaveMetadata()
    {
        var scenario = new Scenario();
        scenario.Files.FailWrite = true;

        var result = await scenario.Service.Upload("a.txt", "text/plain", 1, new MemoryStream([42]));

        Assert.Equal(Failure.Unavailable, result.Failure);
        Assert.Empty(scenario.Store.Documents);
        Assert.Equal(0, scenario.Store.SaveCount);
        Assert.DoesNotContain("internal", result.Message!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RF_DOC_01_MetadataFailure_CompensatesOnlyNewFile(bool cleanupSucceeds)
    {
        var scenario = new Scenario();
        var existingKey = Guid.NewGuid().ToString("N");
        scenario.Files.Keys.Add(existingKey);
        scenario.Store.FailSave = true;
        scenario.Files.RemovalSucceeds = cleanupSucceeds;

        var result = await scenario.Service.Upload("a.txt", "text/plain", 1, new MemoryStream([42]));

        Assert.Equal(Failure.Unavailable, result.Failure);
        Assert.Contains(existingKey, scenario.Files.Keys);
        Assert.Equal(cleanupSucceeds ? 1 : 2, scenario.Files.Keys.Count);
        Assert.Equal(1, scenario.Files.RemovalCount);
        if (!cleanupSucceeds) Assert.Contains("revisión", result.Message!);
    }

    [Fact]
    public async Task RF_DOC_01_CanceledSave_CompensatesAndPropagatesCancellation()
    {
        var scenario = new Scenario();
        scenario.Store.CancelSave = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scenario.Service.Upload("a.txt", "text/plain", 1, new MemoryStream([42])));

        Assert.Empty(scenario.Files.Keys);
        Assert.Equal(1, scenario.Files.RemovalCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RF_DOC_02_List_PassesActorVisibilityToStore(bool administrator)
    {
        var scenario = new Scenario(new(Guid.NewGuid(), administrator));
        scenario.Add(scenario.Actor.Value!.Id);
        scenario.Add(Guid.NewGuid());
        var deleted = scenario.Add(scenario.Actor.Value.Id);
        deleted.Delete(deleted.UploadedBy, false);

        var result = await scenario.Service.List();

        Assert.Equal(Failure.None, result.Failure);
        Assert.Equal(administrator ? 2 : 1, result.Value!.Count);
        Assert.Equal(scenario.Actor.Value.Id, scenario.Store.LastActorId);
        Assert.Equal(administrator, scenario.Store.LastAdministrator);
        Assert.DoesNotContain(result.Value, item => item.Id == deleted.Id);
    }

    [Theory]
    [InlineData("download")]
    [InlineData("delete")]
    public async Task RF_DOC_03_RF_DOC_04_StrangerCannotAccess(string operation)
    {
        var scenario = new Scenario();
        var document = scenario.Add(Guid.NewGuid());

        var failure = operation == "download"
            ? (await scenario.Service.Download(document.Id)).Failure
            : (await scenario.Service.Delete(document.Id)).Failure;

        Assert.Equal(Failure.Forbidden, failure);
        Assert.False(document.IsDeleted);
        Assert.Equal(0, scenario.Files.OpenCount);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RF_DOC_03_RF_DOC_04_OwnerOrAdministratorCanDownloadAndDelete(bool administrator)
    {
        var scenario = new Scenario(new(Guid.NewGuid(), administrator));
        var document = scenario.Add(administrator ? Guid.NewGuid() : scenario.Actor.Value!.Id);

        var download = await scenario.Service.Download(document.Id);
        var deletion = await scenario.Service.Delete(document.Id);

        Assert.Equal(Failure.None, download.Failure);
        Assert.NotNull(download.Value);
        await download.Value.Content.DisposeAsync();
        Assert.Equal(Failure.None, deletion.Failure);
        Assert.True(document.IsDeleted);
        Assert.Contains(document.StorageKey, scenario.Files.Keys);
        Assert.Equal(0, scenario.Files.RemovalCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RF_DOC_04_MissingOrDeleted_IsNotAvailable(bool deleted)
    {
        var scenario = new Scenario();
        var id = Guid.NewGuid();
        if (deleted)
        {
            var document = scenario.Add(scenario.Actor.Value!.Id);
            document.Delete(document.UploadedBy, false);
            id = document.Id;
        }

        Assert.Equal(Failure.NotFound, (await scenario.Service.Download(id)).Failure);
        Assert.Equal(Failure.NotFound, (await scenario.Service.Delete(id)).Failure);
        Assert.Equal(0, scenario.Files.OpenCount);
    }

    [Fact]
    public async Task RD_08_MissingContent_ReturnsControlledFailure()
    {
        var scenario = new Scenario();
        var document = scenario.Add(scenario.Actor.Value!.Id);
        scenario.Files.Keys.Clear();

        var result = await scenario.Service.Download(document.Id);

        Assert.Equal(Failure.Unavailable, result.Failure);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("download")]
    [InlineData("delete")]
    public async Task RD_08_MetadataReadFailure_ReturnsControlledFailure(string operation)
    {
        var scenario = new Scenario();
        scenario.Store.FailRead = true;

        Assert.Equal(Failure.Unavailable, await Run(scenario, operation));
    }

    private static async Task<Failure> Run(Scenario scenario, string operation) => operation switch
    {
        "upload" => (await scenario.Service.Upload("a.txt", "text/plain", 1, new MemoryStream([42]))).Failure,
        "list" => (await scenario.Service.List()).Failure,
        "download" => (await scenario.Service.Download(Guid.NewGuid())).Failure,
        _ => (await scenario.Service.Delete(Guid.NewGuid())).Failure
    };

    private sealed class Scenario
    {
        public ActorStub Actor { get; }
        public StoreStub Store { get; } = new();
        public FilesStub Files { get; } = new();
        public DocumentService Service { get; }

        public Scenario() : this(new Actor(Guid.NewGuid(), false)) { }

        public Scenario(Actor? actor)
        {
            Actor = new(actor);
            Service = new(Store, Files, Actor, new FixedClock());
        }

        public Document Add(Guid owner)
        {
            var document = Document.Create("a.txt", "text/plain", 1, owner, FixedClock.Instant);
            Store.Documents.Add(document);
            Files.Keys.Add(document.StorageKey);
            return document;
        }
    }

    private sealed class ActorStub(Actor? value) : ICurrentActor
    {
        public Actor? Value { get; } = value;
        public Actor? Get() => Value;
    }

    private sealed class FixedClock : TimeProvider
    {
        public static DateTimeOffset Instant { get; } = new(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Instant;
    }

    private sealed class StoreStub : IDocumentStore
    {
        public List<Document> Documents { get; } = [];
        public bool FailSave { get; set; }
        public bool FailRead { get; set; }
        public bool CancelSave { get; set; }
        public int SaveCount { get; private set; }
        public int ReadCount { get; private set; }
        public Guid LastActorId { get; private set; }
        public bool LastAdministrator { get; private set; }

        public Task<IReadOnlyList<Document>> ListVisible(Guid actorId, bool isAdministrator, CancellationToken cancellationToken)
        {
            CheckRead();
            LastActorId = actorId;
            LastAdministrator = isAdministrator;
            return Task.FromResult<IReadOnlyList<Document>>(Documents.Where(item => !item.IsDeleted
                && (isAdministrator || item.UploadedBy == actorId)).ToArray());
        }

        public Task<Document?> Find(Guid id, CancellationToken cancellationToken)
        {
            CheckRead();
            return Task.FromResult(Documents.SingleOrDefault(item => item.Id == id));
        }

        public void Add(Document document) => Documents.Add(document);

        public Task Save(CancellationToken cancellationToken)
        {
            if (CancelSave) throw new OperationCanceledException();
            if (FailSave) throw new DocumentStorageException("internal database detail");
            SaveCount++;
            return Task.CompletedTask;
        }

        private void CheckRead()
        {
            ReadCount++;
            if (FailRead) throw new DocumentStorageException("internal database detail");
        }
    }

    private sealed class FilesStub : IDocumentFiles
    {
        public HashSet<string> Keys { get; } = [];
        public bool FailWrite { get; set; }
        public bool RemovalSucceeds { get; set; } = true;
        public int RemovalCount { get; private set; }
        public int OpenCount { get; private set; }

        public Task Write(string storageKey, Stream content, long expectedSize, CancellationToken cancellationToken)
        {
            if (FailWrite) throw new DocumentStorageException("internal disk detail");
            Keys.Add(storageKey);
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenRead(string storageKey, CancellationToken cancellationToken)
        {
            OpenCount++;
            return Task.FromResult<Stream?>(Keys.Contains(storageKey) ? new MemoryStream([42]) : null);
        }

        public Task<bool> RemoveNew(string storageKey, CancellationToken cancellationToken)
        {
            RemovalCount++;
            if (RemovalSucceeds) Keys.Remove(storageKey);
            return Task.FromResult(RemovalSucceeds);
        }
    }
}
