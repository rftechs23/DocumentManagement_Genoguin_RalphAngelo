namespace DocumentManagement.Domain.Entities;

public sealed class Document
{
    public Guid Id { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string StorageFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; }
}
