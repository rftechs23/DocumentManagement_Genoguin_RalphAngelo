namespace DocumentManagement.Application.Abstractions;

public interface IFileStorage
{
    Task<StoredFileInfo> SaveAsync(
        Stream content,
        long maximumFileSize,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string storageFileName,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string storageFileName,
        CancellationToken cancellationToken = default);

    Task<IStagedFileDeletion?> StageDeleteAsync(
        string storageFileName,
        CancellationToken cancellationToken = default);
}

public sealed record StoredFileInfo(string StorageFileName, long FileSize);

public interface IStagedFileDeletion
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(CancellationToken cancellationToken = default);
}
