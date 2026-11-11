namespace TallerOs.Domain.Documents;

public sealed class DocumentRuleException(string message) : Exception(message);

public sealed class Document
{
    public const long MaximumSize = 10 * 1024 * 1024;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long Size { get; private set; }
    public DateTimeOffset UploadedUtc { get; private set; }
    public Guid UploadedBy { get; private set; }
    public string StorageKey { get; private set; } = "";
    public bool IsDeleted { get; private set; }

    private Document() { }

    public static Document Create(string? name, string? contentType, long size, Guid uploadedBy,
        DateTimeOffset uploadedUtc)
    {
        name = name?.Trim();
        contentType = contentType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or ".."
            || name.Any(character => char.IsControl(character) || character is '/' or '\\'))
            throw new DocumentRuleException("El nombre del documento no es válido; admite hasta 255 caracteres.");
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Length > 127)
            throw new DocumentRuleException("El tipo del documento no es válido.");
        var mediaParts = contentType.Split('/');
        if (mediaParts.Length != 2 || mediaParts.Any(part => part.Length == 0
            || part.Any(character => !char.IsAsciiLetterOrDigit(character)
                && !"!#$&^_.+-".Contains(character))))
            throw new DocumentRuleException("El tipo del documento no es válido.");
        if (size <= 0 || size > MaximumSize)
            throw new DocumentRuleException("El archivo debe contener datos y no superar 10 MiB.");
        if (uploadedBy == Guid.Empty)
            throw new DocumentRuleException("El usuario del documento no es válido.");

        return new Document
        {
            Id = Guid.NewGuid(), Name = name, ContentType = contentType, Size = size,
            UploadedBy = uploadedBy, UploadedUtc = uploadedUtc.ToUniversalTime(),
            StorageKey = Guid.NewGuid().ToString("N")
        };
    }

    public bool CanAccess(Guid actorId, bool isAdministrator) =>
        actorId != Guid.Empty && (UploadedBy == actorId || isAdministrator);

    public void Delete(Guid actorId, bool isAdministrator)
    {
        if (!CanAccess(actorId, isAdministrator))
            throw new DocumentRuleException("No tienes acceso a este documento.");
        IsDeleted = true;
    }
}
