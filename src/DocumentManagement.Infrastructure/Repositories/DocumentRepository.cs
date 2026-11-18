using DocumentManagement.Application.Abstractions;
using DocumentManagement.Domain.Entities;
using DocumentManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Infrastructure.Repositories;

public sealed class DocumentRepository(DocumentDbContext dbContext) : IDocumentRepository
{
    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(document => document.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Document>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Documents
            .AsNoTracking()
            .OrderByDescending(document => document.UploadedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        await dbContext.Documents.AddAsync(document, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.Documents
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (document is null)
        {
            return false;
        }

        dbContext.Documents.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
