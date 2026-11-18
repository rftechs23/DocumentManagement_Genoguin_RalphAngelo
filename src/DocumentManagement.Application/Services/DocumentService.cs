using DocumentManagement.Application.Abstractions;
using DocumentManagement.Application.DTOs;
using DocumentManagement.Application.Exceptions;
using DocumentManagement.Domain.Entities;
using System.Net.Http.Headers;

namespace DocumentManagement.Application.Services;

public sealed class DocumentService(
    IDocumentRepository documentRepository,
    IFileStorage fileStorage) : IDocumentService
{
    public const long MaximumFileSizeBytes = 10 * 1024 * 1024;

    public async Task<DocumentMetadataDto> UploadAsync(
        UploadDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Content is null)
        {
            throw new InvalidDocumentUploadException("A file is required.");
        }

        if (request.FileSize <= 0)
        {
            throw new InvalidDocumentUploadException("Empty files are not supported.");
        }

        if (request.FileSize > MaximumFileSizeBytes)
        {
            throw new DocumentSizeLimitExceededException(MaximumFileSizeBytes);
        }

        var originalFileName = NormalizeFileName(request.OriginalFileName);
        var storedFile = await fileStorage.SaveAsync(
            request.Content,
            MaximumFileSizeBytes,
            cancellationToken);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            OriginalFileName = originalFileName,
            StorageFileName = storedFile.StorageFileName,
            ContentType = NormalizeContentType(request.ContentType),
            FileSize = storedFile.FileSize,
            UploadedAtUtc = DateTimeOffset.UtcNow
        };

        try
        {
            await documentRepository.AddAsync(document, cancellationToken);
        }
        catch (Exception persistenceException)
        {
            try
            {
                await fileStorage.DeleteAsync(storedFile.StorageFileName, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "Saving document metadata failed, and the stored file could not be removed.",
                    persistenceException,
                    cleanupException);
            }

            throw;
        }

        return ToMetadataDto(document);
    }

    public async Task<IReadOnlyList<DocumentMetadataDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var documents = await documentRepository.ListAsync(cancellationToken);
        return documents.Select(ToMetadataDto).ToArray();
    }

    public async Task<DocumentMetadataDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await documentRepository.GetByIdAsync(id, cancellationToken);
        return document is null ? null : ToMetadataDto(document);
    }

    public async Task<DocumentDownloadDto?> DownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var content = await fileStorage.OpenReadAsync(document.StorageFileName, cancellationToken);
        if (content is null)
        {
            throw new DocumentFileMissingException(id);
        }

        return new DocumentDownloadDto(
            content,
            document.OriginalFileName,
            document.ContentType,
            document.FileSize);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return false;
        }

        var stagedDeletion = await fileStorage.StageDeleteAsync(
            document.StorageFileName,
            cancellationToken);

        bool deleted;
        try
        {
            deleted = await documentRepository.DeleteAsync(id, cancellationToken);
        }
        catch (Exception persistenceException)
        {
            await RestoreOrThrowAsync(stagedDeletion, persistenceException);
            throw;
        }

        if (!deleted)
        {
            await RestoreOrThrowAsync(stagedDeletion, null);
            return false;
        }

        if (stagedDeletion is null)
        {
            return true;
        }

        try
        {
            await stagedDeletion.CommitAsync(cancellationToken);
            return true;
        }
        catch (Exception cleanupException)
        {
            try
            {
                await stagedDeletion.RestoreAsync(CancellationToken.None);
                await documentRepository.AddAsync(document, CancellationToken.None);
            }
            catch (Exception compensationException)
            {
                throw new AggregateException(
                    "Finalizing document deletion failed, and restoring the document also failed.",
                    cleanupException,
                    compensationException);
            }

            throw;
        }
    }

    private static async Task RestoreOrThrowAsync(
        IStagedFileDeletion? stagedDeletion,
        Exception? operationException)
    {
        if (stagedDeletion is null)
        {
            return;
        }

        try
        {
            await stagedDeletion.RestoreAsync(CancellationToken.None);
        }
        catch (Exception restoreException)
        {
            if (operationException is not null)
            {
                throw new AggregateException(
                    "The database operation failed, and the staged file could not be restored.",
                    operationException,
                    restoreException);
            }

            throw;
        }
    }

    private static string NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidDocumentUploadException("A filename is required.");
        }

        var normalized = Path.GetFileName(
            fileName.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar));
        normalized = new string(normalized.Where(character => !char.IsControl(character)).ToArray()).Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidDocumentUploadException("A valid filename is required.");
        }

        if (normalized.Length > 255)
        {
            throw new InvalidDocumentUploadException("The filename cannot exceed 255 characters.");
        }

        return normalized;
    }

    private static string NormalizeContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && MediaTypeHeaderValue.TryParse(contentType, out _)
            ? contentType
            : "application/octet-stream";

    private static DocumentMetadataDto ToMetadataDto(Document document) =>
        new(
            document.Id,
            document.OriginalFileName,
            document.ContentType,
            document.FileSize,
            document.UploadedAtUtc);
}
