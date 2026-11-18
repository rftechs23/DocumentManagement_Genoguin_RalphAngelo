namespace DocumentManagement.Application.DTOs;

public sealed record UploadDocumentRequest(
    Stream Content,
    string OriginalFileName,
    string? ContentType,
    long FileSize);
