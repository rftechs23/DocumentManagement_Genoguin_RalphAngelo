using DocumentManagement.Application.DTOs;

namespace DocumentManagement.Application.Services;

public interface IDocumentService
{
    Task<DocumentMetadataDto> UploadAsync(
        UploadDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentMetadataDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<DocumentMetadataDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DocumentDownloadDto?> DownloadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
