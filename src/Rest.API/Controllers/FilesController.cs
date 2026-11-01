using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Application.Features.V1.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rest.API.Extensions;

namespace Rest.API.Controllers;

[ApiController]
[Route("api/v1/files")]
[Produces("application/json")]
[Authorize]
[ResponseType<UploadDocumentResponse>]
[ResponseType<GetFileResponse>]
[ResponseType<FileContentResponse>]
public sealed class FilesController(IDispatcher dispatcher) : ControllerBase
{
    private const long UploadRequestSizeLimitBytes = 52_428_800;

    /// <summary>
    /// Only multipart upload is used here; the JSON contract is still returned so
    /// the client always receives the common envelope.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(UploadRequestSizeLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestSizeLimitBytes)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Result<UploadDocumentResponse>
                .Failure(AppStatusCodes.BadRequest, "A document file is required.")
                .ToActionResult();
        }

        Stream content = file.OpenReadStream();
        if (!content.CanSeek)
        {
            var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            await content.DisposeAsync();
            buffer.Position = 0;
            content = buffer;
        }

        await using (content)
        {
            var result = await dispatcher.ExecuteCommand(
                new UploadDocumentCommand(content, file.FileName, file.ContentType),
                cancellationToken);

            return result.ToActionResult();
        }
    }

    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> GetMetadata(Guid fileId, CancellationToken cancellationToken)
    {
        var result = await dispatcher.ExecuteQuery(
            new GetFileQuery(fileId),
            cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Streams the validated document. This is the only endpoint that returns a
    /// binary response; failures still use the JSON envelope.
    /// </summary>
    [HttpGet("{fileId:guid}/content")]
    [Produces("application/octet-stream", "application/pdf", "text/plain")]
    public async Task<IActionResult> Download(Guid fileId, CancellationToken cancellationToken)
    {
        var result = await dispatcher.ExecuteQuery(
            new GetFileContentQuery(fileId),
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            return result.ToActionResult();
        }

        return File(
            result.Data.Content,
            result.Data.ContentType,
            result.Data.FileName,
            enableRangeProcessing: true);
    }
}
