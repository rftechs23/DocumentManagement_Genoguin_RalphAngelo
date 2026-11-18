namespace DocumentManagement.Application.DTOs;

public sealed record DocumentMetadataDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    DateTimeOffset UploadedAtUtc);
