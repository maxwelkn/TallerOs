using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallerOs.Application.Documents;
using TallerOs.Domain.Documents;
using TallerOs.Infrastructure.Documents;

namespace TallerOs.IntegrationTests;

public sealed class DocumentHttpTests(WorkshopHttpFixture api) : IClassFixture<WorkshopHttpFixture>
{
    public static IEnumerable<object[]> ProtectedRoutes()
    {
        yield return ["POST", "/api/documents"];
        yield return ["GET", "/api/documents"];
        yield return ["GET", $"/api/documents/{Guid.NewGuid()}/download"];
        yield return ["DELETE", $"/api/documents/{Guid.NewGuid()}"];
    }

    public static IEnumerable<object[]> InvalidSessions()
    {
        foreach (var route in ProtectedRoutes())
            foreach (var session in new[] { "unknown", "expired", "revoked", "disabled" })
                yield return [route[0], route[1], session];
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task RD_06_EveryRouteRequiresSession(string method, string route)
    {
        using var response = await api.Send(method, route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(InvalidSessions))]
    public async Task RD_06_EveryRouteRejectsInvalidSession(string method, string route, string session)
    {
        using var response = await api.Send(method, route, session);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RF_DOC_01_UploadReturnsMetadataAndNoInternalStorageDetails()
    {
        using var form = Form([0, 255, 42], "manual.bin", "application/octet-stream");
        form.Add(new StringContent(Guid.NewGuid().ToString()), "uploadedBy");
        using var me = await api.Send("GET", "/api/access/me", "standard");
        var actor = await me.Content.ReadFromJsonAsync<JsonElement>();

        using var response = await api.Send("POST", "/api/documents", "standard", content: form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<DocumentView>();
        Assert.NotNull(document);
        Assert.Equal("manual.bin", document.Name);
        Assert.Equal("application/octet-stream", document.ContentType);
        Assert.Equal(3, document.Size);
        Assert.Equal(actor.GetProperty("id").GetGuid(), document.UploadedBy);
        Assert.Equal(TimeSpan.Zero, document.UploadedUtc.Offset);
        Assert.Equal($"/api/documents/{document.Id}/download", response.Headers.Location!.OriginalString);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("storageKey", json);
        Assert.DoesNotContain("path", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RF_DOC_02_ListExcludesOtherOwnersAndAdministratorSeesBoth()
    {
        var own = await Upload("standard");
        var other = await Upload("reader");

        using var mineResponse = await api.Send("GET", "/api/documents", "standard");
        using var adminResponse = await api.Send("GET", "/api/documents", "administrator");
        var mine = await mineResponse.Content.ReadFromJsonAsync<DocumentView[]>();
        var all = await adminResponse.Content.ReadFromJsonAsync<DocumentView[]>();

        Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Contains(mine!, item => item.Id == own.Id);
        Assert.DoesNotContain(mine!, item => item.Id == other.Id);
        Assert.Contains(all!, item => item.Id == own.Id);
        Assert.Contains(all!, item => item.Id == other.Id);
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("administrator")]
    public async Task RF_DOC_03_OwnerOrAdministratorDownloadsIdenticalBytes(string role)
    {
        var bytes = new byte[] { 0, 255, 42, 128 };
        var document = await Upload("standard", bytes);

        using var response = await api.Send("GET", $"/api/documents/{document.Id}/download", role);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Theory]
    [InlineData("GET", "/download")]
    [InlineData("DELETE", "")]
    public async Task RF_DOC_03_RF_DOC_04_StrangerIsRejectedAndDocumentRemains(string method, string suffix)
    {
        var document = await Upload("standard");

        using var response = await api.Send(method, $"/api/documents/{document.Id}{suffix}", "reader");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var ownDownload = await api.Send("GET", $"/api/documents/{document.Id}/download", "standard");
        Assert.Equal(HttpStatusCode.OK, ownDownload.StatusCode);
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("administrator")]
    public async Task RF_DOC_04_DeleteIsLogicalAndBlocksDirectDownloadForEveryone(string role)
    {
        var document = await Upload("standard");

        using var response = await api.Send("DELETE", $"/api/documents/{document.Id}", role);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        foreach (var viewer in new[] { "standard", "administrator" })
        {
            using var list = await api.Send("GET", "/api/documents", viewer);
            Assert.DoesNotContain((await list.Content.ReadFromJsonAsync<DocumentView[]>())!, item => item.Id == document.Id);
            using var download = await api.Send("GET", $"/api/documents/{document.Id}/download", viewer);
            Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        }
        using var repeated = await api.Send("DELETE", $"/api/documents/{document.Id}", role);
        Assert.Equal(HttpStatusCode.NotFound, repeated.StatusCode);
        await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseSqlite($"Data Source={api.DocumentsDatabasePath};Pooling=False").Options);
        var row = await db.Documents.SingleAsync(item => item.Id == document.Id);
        Assert.True(row.IsDeleted);
        Assert.Equal(document.Size, new FileInfo(Path.Combine(api.DocumentsDirectory, row.StorageKey)).Length);
    }

    [Theory]
    [InlineData("GET", "/download")]
    [InlineData("DELETE", "")]
    public async Task RD_07_MissingDocument_Returns404(string method, string suffix)
    {
        using var response = await api.Send(method, $"/api/documents/{Guid.NewGuid()}{suffix}", "standard");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("empty")]
    [InlineData("multiple")]
    [InlineData("wrong-field")]
    [InlineData("bad-name")]
    [InlineData("bad-type")]
    public async Task RD_07_InvalidMultipart_ReturnsControlled400(string kind)
    {
        using var form = new MultipartFormDataContent();
        if (kind != "none")
        {
            var file = new ByteArrayContent(kind == "empty" ? [] : [42]);
            file.Headers.TryAddWithoutValidation("Content-Type", kind == "bad-type" ? "invalid" : "text/plain");
            form.Add(file, kind == "wrong-field" ? "other" : "file", kind == "bad-name" ? ".." : "a.txt");
        }
        if (kind == "multiple") form.Add(new ByteArrayContent([7]), "file", "b.txt");

        using var response = await api.Send("POST", "/api/documents", "standard", content: form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("message", out _));
    }

    [Fact]
    public async Task RD_07_NonMultipartAndMissingBoundary_AreControlled()
    {
        using var json = await api.Send("POST", "/api/documents", "standard", "{}");
        using var content = new StringContent("invalid", Encoding.UTF8, "multipart/form-data");
        using var malformed = await api.Send("POST", "/api/documents", "standard", content: content);

        Assert.Equal(HttpStatusCode.BadRequest, json.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Theory]
    [InlineData("../../manual.txt")]
    [InlineData("C:\\folder\\manual.txt")]
    public async Task RD_06_ClientPathIsOnlyANameAndNeverControlsStorage(string name)
    {
        using var form = Form([42], name);

        using var response = await api.Send("POST", "/api/documents", "standard", content: form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<DocumentView>();
        Assert.Equal("manual.txt", document!.Name);
        Assert.False(File.Exists(Path.Combine(api.DocumentsDirectory, "manual.txt")));
    }

    [Fact]
    public async Task RD_07_MaximumSizeIsAcceptedAndLargerFileIsRejected()
    {
        var maximum = new byte[Document.MaximumSize];
        maximum[^1] = 42;
        var uploaded = await Upload("standard", maximum);
        using var larger = Form(new byte[Document.MaximumSize + 1]);

        using var response = await api.Send("POST", "/api/documents", "standard", content: larger);

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge);
        using var downloaded = await api.Send("GET", $"/api/documents/{uploaded.Id}/download", "standard");
        Assert.Equal(maximum, await downloaded.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task RD_08_MissingPhysicalContent_IsControlledWithoutInternalPaths()
    {
        var document = await Upload("standard");
        await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseSqlite($"Data Source={api.DocumentsDatabasePath};Pooling=False").Options);
        var row = await db.Documents.SingleAsync(item => item.Id == document.Id);
        File.Delete(Path.Combine(api.DocumentsDirectory, row.StorageKey));

        using var response = await api.Send("GET", $"/api/documents/{document.Id}/download", "standard");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain(api.DocumentsDirectory, await response.Content.ReadAsStringAsync());
        using var health = await api.Send("GET", "/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private async Task<DocumentView> Upload(string role, byte[]? bytes = null)
    {
        using var form = Form(bytes ?? [42]);
        using var response = await api.Send("POST", "/api/documents", role, content: form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Assert.IsType<DocumentView>(await response.Content.ReadFromJsonAsync<DocumentView>());
    }

    private static MultipartFormDataContent Form(byte[] bytes, string name = "a.txt", string type = "text/plain")
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(content, "file", name);
        return form;
    }
}
