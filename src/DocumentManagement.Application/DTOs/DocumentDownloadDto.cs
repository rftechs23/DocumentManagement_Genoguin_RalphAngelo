namespace DocumentManagement.Application.DTOs;

public sealed record DocumentDownloadDto(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long FileSize);
