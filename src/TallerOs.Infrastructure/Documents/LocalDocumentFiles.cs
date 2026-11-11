using Microsoft.Extensions.Logging;
using TallerOs.Application.Documents;
using TallerOs.Domain.Documents;

namespace TallerOs.Infrastructure.Documents;

public sealed class LocalDocumentFiles(string directory, ILogger<LocalDocumentFiles> logger) : IDocumentFiles
{
    public async Task Write(string storageKey, Stream content, long expectedSize, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        var created = false;
        try
        {
            Directory.CreateDirectory(directory);
            await using var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 65536, FileOptions.Asynchronous);
            created = true;
            var buffer = new byte[65536];
            long size = 0;
            int count;
            while ((count = await content.ReadAsync(buffer, cancellationToken)) != 0)
            {
                size += count;
                if (size > Document.MaximumSize || size > expectedSize)
                    throw new DocumentRuleException("El tamaño real del archivo no es válido; el máximo es 10 MiB.");
                await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
            if (size == 0 || size != expectedSize)
                throw new DocumentRuleException("El tamaño real del archivo no coincide con el declarado.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or DocumentRuleException or OperationCanceledException)
        {
            if (created) await RemoveNew(storageKey, CancellationToken.None);
            if (error is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(error, "Falló la escritura de un documento.");
                throw new DocumentStorageException("No se pudo escribir el documento.", error);
            }
            throw;
        }
    }

    public Task<Stream?> OpenRead(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            Stream content = new FileStream(Resolve(storageKey), FileMode.Open, FileAccess.Read,
                FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult<Stream?>(content);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(error, "Falló la lectura de un documento.");
            throw new DocumentStorageException("No se pudo leer el documento.", error);
        }
    }

    public Task<bool> RemoveNew(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            File.Delete(Resolve(storageKey));
            return Task.FromResult(true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogError(error, "No se pudo retirar un archivo nuevo sin registrar.");
            return Task.FromResult(false);
        }
    }

    private string Resolve(string storageKey)
    {
        if (!Guid.TryParseExact(storageKey, "N", out _))
            throw new DocumentStorageException("Identificador de almacenamiento no válido.");
        return Path.Combine(directory, storageKey);
    }
}
