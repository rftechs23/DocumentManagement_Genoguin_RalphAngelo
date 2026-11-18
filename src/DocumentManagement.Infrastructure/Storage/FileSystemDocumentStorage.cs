using DocumentManagement.Application.Abstractions;
using DocumentManagement.Application.Exceptions;

namespace DocumentManagement.Infrastructure.Storage;

public sealed class FileSystemDocumentStorage : IFileStorage
{
    private const string DeletionDirectoryName = ".deleting";
    private const int BufferSize = 81920;
    private readonly string _rootPath;
    private readonly string _deletionPath;

    public FileSystemDocumentStorage(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath, Directory.GetCurrentDirectory());
        _deletionPath = Path.Combine(_rootPath, DeletionDirectoryName);
    }

    public async Task<StoredFileInfo> SaveAsync(
        Stream content,
        long maximumFileSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead)
        {
            throw new InvalidDocumentUploadException("The uploaded file cannot be read.");
        }

        if (maximumFileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFileSize));
        }

        Directory.CreateDirectory(_rootPath);

        var storageFileName = Guid.NewGuid().ToString("N");
        var destinationPath = ResolveFilePath(storageFileName);
        long totalBytes = 0;
        var buffer = new byte[BufferSize];

        try
        {
            await using (var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                int bytesRead;
                while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > maximumFileSize)
                    {
                        throw new DocumentSizeLimitExceededException(maximumFileSize);
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }
            }

            if (totalBytes == 0)
            {
                throw new InvalidDocumentUploadException("Empty files are not supported.");
            }

            return new StoredFileInfo(storageFileName, totalBytes);
        }
        catch (Exception saveException)
        {
            try
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "Saving the uploaded file failed, and its partial file could not be removed.",
                    saveException,
                    cleanupException);
            }

            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(
        string storageFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var filePath = ResolveFilePath(storageFileName);
        if (!File.Exists(filePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        try
        {
            Stream stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult<Stream?>(stream);
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task<bool> DeleteAsync(
        string storageFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var filePath = ResolveFilePath(storageFileName);
        if (!File.Exists(filePath))
        {
            return Task.FromResult(false);
        }

        File.Delete(filePath);
        return Task.FromResult(true);
    }

    public Task<IStagedFileDeletion?> StageDeleteAsync(
        string storageFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourcePath = ResolveFilePath(storageFileName);
        if (!File.Exists(sourcePath))
        {
            return Task.FromResult<IStagedFileDeletion?>(null);
        }

        Directory.CreateDirectory(_deletionPath);
        var stagedPath = Path.Combine(_deletionPath, $"{Guid.NewGuid():N}.pending");
        File.Move(sourcePath, stagedPath);
        return Task.FromResult<IStagedFileDeletion?>(
            new StagedFileDeletion(sourcePath, stagedPath));
    }

    private string ResolveFilePath(string storageFileName)
    {
        if (!Guid.TryParseExact(storageFileName, "N", out _))
        {
            throw new InvalidOperationException("The storage identifier is invalid.");
        }

        return Path.Combine(_rootPath, storageFileName);
    }

    private sealed class StagedFileDeletion(string sourcePath, string stagedPath) : IStagedFileDeletion
    {
        private bool _completed;

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_completed)
            {
                throw new InvalidOperationException("The staged deletion has already completed.");
            }

            if (!File.Exists(stagedPath))
            {
                throw new FileNotFoundException("The staged document file could not be found.", stagedPath);
            }

            File.Delete(stagedPath);
            _completed = true;
            return Task.CompletedTask;
        }

        public Task RestoreAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_completed)
            {
                throw new InvalidOperationException("The staged deletion has already completed.");
            }

            if (!File.Exists(stagedPath))
            {
                throw new FileNotFoundException("The staged document file could not be found.", stagedPath);
            }

            File.Move(stagedPath, sourcePath);
            _completed = true;
            return Task.CompletedTask;
        }
    }
}
