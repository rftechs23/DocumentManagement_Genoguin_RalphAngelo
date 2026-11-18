namespace DocumentManagement.Application.Exceptions;

public sealed class InvalidDocumentUploadException(string message) : Exception(message);
