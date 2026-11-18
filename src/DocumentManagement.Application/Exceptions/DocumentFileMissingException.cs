namespace DocumentManagement.Application.Exceptions;

public sealed class DocumentFileMissingException(Guid documentId)
    : Exception($"The stored file for document '{documentId}' could not be found.");
