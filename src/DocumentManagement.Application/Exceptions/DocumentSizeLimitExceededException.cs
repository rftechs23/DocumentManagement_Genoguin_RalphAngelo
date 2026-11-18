namespace DocumentManagement.Application.Exceptions;

public sealed class DocumentSizeLimitExceededException(long maximumSizeBytes)
    : Exception($"The uploaded file exceeds the maximum size of {maximumSizeBytes} bytes.")
{
    public long MaximumSizeBytes { get; } = maximumSizeBytes;
}
