using TallerOs.Domain.Documents;

namespace TallerOs.UnitTests;

public sealed class DocumentTests
{
    [Fact]
    public void RF_DOC_01_Create_RecordsNormalizedMetadataAndUtc()
    {
        var owner = Guid.NewGuid();
        var instant = new DateTimeOffset(2026, 11, 10, 20, 0, 0, TimeSpan.FromHours(-4));

        var document = Document.Create(" Manual.pdf ", " APPLICATION/PDF ", 123, owner, instant);

        Assert.Equal("Manual.pdf", document.Name);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(123, document.Size);
        Assert.Equal(owner, document.UploadedBy);
        Assert.Equal(instant.ToUniversalTime(), document.UploadedUtc);
        Assert.Equal(TimeSpan.Zero, document.UploadedUtc.Offset);
        Assert.NotEqual(Guid.Empty, document.Id);
        Assert.True(Guid.TryParseExact(document.StorageKey, "N", out _));
        Assert.False(document.IsDeleted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../secret")]
    [InlineData("C:\\secret")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("bad\nname")]
    public void RF_DOC_01_InvalidName_IsRejected(string? name) =>
        Assert.Throws<DocumentRuleException>(() => Create(name: name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("text")]
    [InlineData("text/")]
    [InlineData("/plain")]
    [InlineData("text/plain/extra")]
    [InlineData("text/plain\r\nmalicious")]
    public void RF_DOC_01_InvalidType_IsRejected(string? type) =>
        Assert.Throws<DocumentRuleException>(() => Create(type: type));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Document.MaximumSize + 1)]
    public void RD_07_InvalidSize_IsRejected(long size) =>
        Assert.Throws<DocumentRuleException>(() => Create(size: size));

    [Fact]
    public void RD_07_MetadataLimitsAndEmptyOwner_AreRejected()
    {
        Assert.Throws<DocumentRuleException>(() => Create(name: new string('x', 256)));
        Assert.Throws<DocumentRuleException>(() => Create(type: "text/" + new string('x', 123)));
        Assert.Throws<DocumentRuleException>(() => Document.Create("a.txt", "text/plain", 1, Guid.Empty, DateTimeOffset.UtcNow));
        Assert.Equal(Document.MaximumSize, Create(size: Document.MaximumSize).Size);
    }

    [Fact]
    public void RF_DOC_04_Delete_ProtectsOwnershipAndPreservesMetadata()
    {
        var document = Create();
        var storageKey = document.StorageKey;

        Assert.Throws<DocumentRuleException>(() => document.Delete(Guid.NewGuid(), false));
        Assert.False(document.IsDeleted);
        document.Delete(document.UploadedBy, false);

        Assert.True(document.IsDeleted);
        Assert.Equal(storageKey, document.StorageKey);
        Assert.Equal("a.txt", document.Name);
        Assert.Equal(1, document.Size);
    }

    [Fact]
    public void RF_DOC_04_AdministratorCanDeleteButAnonymousCannot()
    {
        var document = Create();

        Assert.Throws<DocumentRuleException>(() => document.Delete(Guid.Empty, true));
        document.Delete(Guid.NewGuid(), true);

        Assert.True(document.IsDeleted);
    }

    private static Document Create(string? name = "a.txt", string? type = "text/plain", long size = 1) =>
        Document.Create(name, type, size, Guid.NewGuid(), DateTimeOffset.UtcNow);
}
