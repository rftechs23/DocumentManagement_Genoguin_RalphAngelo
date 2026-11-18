using DocumentManagement.Application.DTOs;

namespace DocumentManagement.Application.Services;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentMetadataDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<DocumentMetadataDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
