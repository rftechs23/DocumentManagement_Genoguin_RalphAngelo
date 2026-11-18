using DocumentManagement.Application.DTOs;
using DocumentManagement.Application.Exceptions;
using DocumentManagement.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(IDocumentService documentService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(DocumentService.MaximumFileSizeBytes + 1024 * 1024)]
    [ProducesResponseType<DocumentMetadataDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<DocumentMetadataDto>> Upload(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "A file is required.",
                "Invalid upload"));
        }

        await using var content = file.OpenReadStream();

        try
        {
            var document = await documentService.UploadAsync(
                new UploadDocumentRequest(content, file.FileName, file.ContentType, file.Length),
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = document.Id },
                document);
        }
        catch (InvalidDocumentUploadException exception)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                exception.Message,
                "Invalid upload"));
        }
        catch (DocumentSizeLimitExceededException exception)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                CreateProblem(
                    StatusCodes.Status413PayloadTooLarge,
                    exception.Message,
                    "File too large"));
        }
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DocumentMetadataDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DocumentMetadataDto>>> List(
        CancellationToken cancellationToken)
    {
        var documents = await documentService.ListAsync(cancellationToken);
        return Ok(documents);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DocumentMetadataDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentMetadataDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await documentService.GetByIdAsync(id, cancellationToken);
        return document is null
            ? NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                $"Document '{id}' was not found.",
                "Document not found"))
            : Ok(document);
    }

    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = await documentService.DownloadAsync(id, cancellationToken);
            if (document is null)
            {
                return NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    $"Document '{id}' was not found.",
                    "Document not found"));
            }

            return File(document.Content, document.ContentType, document.OriginalFileName);
        }
        catch (DocumentFileMissingException exception)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                exception.Message,
                "Stored file not found"));
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await documentService.DeleteAsync(id, cancellationToken);
        return deleted
            ? NoContent()
            : NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                $"Document '{id}' was not found.",
                "Document not found"));
    }

    private static ProblemDetails CreateProblem(int status, string detail, string title) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail
        };
}
