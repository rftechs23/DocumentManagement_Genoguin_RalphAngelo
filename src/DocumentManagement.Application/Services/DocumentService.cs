using DocumentManagement.Application.Abstractions;
using DocumentManagement.Application.DTOs;
using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Application.Services;

public sealed class DocumentService(IDocumentRepository documentRepository) : IDocumentService
{
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

    private static DocumentMetadataDto ToMetadataDto(Document document) =>
        new(
            document.Id,
            document.OriginalFileName,
            document.ContentType,
            document.FileSize,
            document.UploadedAtUtc);
}
