using System.Text;
using DocumentManagement.Application.Abstractions;
using DocumentManagement.Application.DTOs;
using DocumentManagement.Application.Exceptions;
using DocumentManagement.Application.Services;
using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Tests;

public sealed class DocumentServiceTests
{
    [Fact]
    public async Task UploadAsync_StoresFileAndMetadata_AndReturnsPublicMetadata()
    {
        var repository = new InMemoryDocumentRepository();
        var storage = new InMemoryFileStorage();
        var service = new DocumentService(repository, storage);
        var fileBytes = Encoding.UTF8.GetBytes("assessment document");

        var result = await service.UploadAsync(new UploadDocumentRequest(
            new MemoryStream(fileBytes),
            @"..\private\assessment.txt",
            "text/plain",
            fileBytes.Length));

        var savedDocument = Assert.Single(repository.Documents.Values);
        Assert.Equal(result.Id, savedDocument.Id);
        Assert.Equal("assessment.txt", result.OriginalFileName);
        Assert.Equal("text/plain", result.ContentType);
        Assert.Equal(fileBytes.Length, result.FileSize);
        Assert.Equal(fileBytes, storage.Files[savedDocument.StorageFileName]);
        Assert.DoesNotContain("StorageFileName", typeof(DocumentMetadataDto).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsMetadataForExistingDocument()
    {
        var document = CreateDocument();
        var repository = new InMemoryDocumentRepository();
        repository.Documents.Add(document.Id, document);
        var service = new DocumentService(repository, new InMemoryFileStorage());

        var result = await service.GetByIdAsync(document.Id);

        Assert.NotNull(result);
        Assert.Equal(document.Id, result.Id);
        Assert.Equal(document.OriginalFileName, result.OriginalFileName);
        Assert.Equal(document.FileSize, result.FileSize);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenDocumentDoesNotExist()
    {
        var service = new DocumentService(new InMemoryDocumentRepository(), new InMemoryFileStorage());

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UploadAsync_RejectsFileAboveLimitBeforeWritingIt()
    {
        var storage = new InMemoryFileStorage();
        var service = new DocumentService(new InMemoryDocumentRepository(), storage);

        await Assert.ThrowsAsync<DocumentSizeLimitExceededException>(() => service.UploadAsync(
            new UploadDocumentRequest(
                new MemoryStream([1]),
                "oversized.bin",
                "application/octet-stream",
                DocumentService.MaximumFileSizeBytes + 1)));

        Assert.Empty(storage.Files);
        Assert.Equal(0, storage.SaveCalls);
    }

    [Fact]
    public async Task DeleteAsync_RemovesMetadataAndStoredFile()
    {
        var document = CreateDocument();
        var repository = new InMemoryDocumentRepository();
        repository.Documents.Add(document.Id, document);
        var storage = new InMemoryFileStorage();
        storage.Files.Add(document.StorageFileName, [1, 2, 3]);
        var service = new DocumentService(repository, storage);

        var deleted = await service.DeleteAsync(document.Id);

        Assert.True(deleted);
        Assert.Empty(repository.Documents);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task DeleteAsync_RestoresStoredFileIfMetadataDeleteFails()
    {
        var document = CreateDocument();
        var repository = new InMemoryDocumentRepository { ThrowOnDelete = true };
        repository.Documents.Add(document.Id, document);
        var storage = new InMemoryFileStorage();
        var fileBytes = new byte[] { 4, 5, 6 };
        storage.Files.Add(document.StorageFileName, fileBytes);
        var service = new DocumentService(repository, storage);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(document.Id));

        Assert.True(repository.Documents.ContainsKey(document.Id));
        Assert.Equal(fileBytes, storage.Files[document.StorageFileName]);
    }

    [Fact]
    public async Task UploadAsync_RemovesStoredFileIfMetadataSaveFails()
    {
        var repository = new InMemoryDocumentRepository { ThrowOnAdd = true };
        var storage = new InMemoryFileStorage();
        var service = new DocumentService(repository, storage);
        var content = Encoding.UTF8.GetBytes("document");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(
            new UploadDocumentRequest(
                new MemoryStream(content),
                "document.txt",
                "text/plain",
                content.Length)));

        Assert.Empty(storage.Files);
        Assert.Empty(repository.Documents);
    }

    private static Document CreateDocument() => new()
    {
        Id = Guid.NewGuid(),
        OriginalFileName = "existing.txt",
        StorageFileName = Guid.NewGuid().ToString("N"),
        ContentType = "text/plain",
        FileSize = 3,
        UploadedAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class InMemoryDocumentRepository : IDocumentRepository
    {
        public Dictionary<Guid, Document> Documents { get; } = [];

        public bool ThrowOnAdd { get; init; }

        public bool ThrowOnDelete { get; init; }

        public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Documents.GetValueOrDefault(id));

        public Task<IReadOnlyList<Document>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Document>>(
                Documents.Values.OrderByDescending(document => document.UploadedAtUtc).ToArray());

        public Task AddAsync(Document document, CancellationToken cancellationToken = default)
        {
            if (ThrowOnAdd)
            {
                throw new InvalidOperationException("Simulated metadata persistence failure.");
            }

            Documents.Add(document.Id, document);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (ThrowOnDelete)
            {
                throw new InvalidOperationException("Simulated metadata deletion failure.");
            }

            return Task.FromResult(Documents.Remove(id));
        }
    }

    private sealed class InMemoryFileStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public int SaveCalls { get; private set; }

        public async Task<StoredFileInfo> SaveAsync(
            Stream content,
            long maximumFileSize,
            CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length > maximumFileSize)
            {
                throw new DocumentSizeLimitExceededException(maximumFileSize);
            }

            var fileName = Guid.NewGuid().ToString("N");
            Files.Add(fileName, buffer.ToArray());
            return new StoredFileInfo(fileName, buffer.Length);
        }

        public Task<Stream?> OpenReadAsync(
            string storageFileName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(Files.TryGetValue(storageFileName, out var bytes)
                ? new MemoryStream(bytes, writable: false)
                : null);

        public Task<bool> DeleteAsync(
            string storageFileName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Files.Remove(storageFileName));

        public Task<IStagedFileDeletion?> StageDeleteAsync(
            string storageFileName,
            CancellationToken cancellationToken = default)
        {
            if (!Files.Remove(storageFileName, out var bytes))
            {
                return Task.FromResult<IStagedFileDeletion?>(null);
            }

            return Task.FromResult<IStagedFileDeletion?>(new StagedDeletion(Files, storageFileName, bytes));
        }

        private sealed class StagedDeletion(
            Dictionary<string, byte[]> files,
            string storageFileName,
            byte[] bytes) : IStagedFileDeletion
        {
            private bool _completed;

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _completed = true;
                return Task.CompletedTask;
            }

            public Task RestoreAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_completed)
                {
                    files[storageFileName] = bytes;
                    _completed = true;
                }

                return Task.CompletedTask;
            }
        }
    }
}
