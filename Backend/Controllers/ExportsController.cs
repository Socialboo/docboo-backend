using Application.Exports;
using DTOs.Exports;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// Export of works to .docx and .pdf. Exports run in the background: create a job, poll its status,
/// then download the file.
/// </summary>
[ApiController]
public sealed class ExportsController(ExportService exports) : ControllerBase
{
    /// <summary>Queues an export of the work's current document.</summary>
    /// <remarks>Returns 400 with the missing data (by path) when the work is not ready to be exported.</remarks>
    [HttpPost("api/works/{workId:guid}/exports", Name = "CreateExport")]
    [Consumes("application/json")]
    [ProducesResponseType<ExportJobResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ExportJobResponse>> Create(Guid workId, CreateExportRequest request, CancellationToken ct)
    {
        var result = await exports.CreateAsync(workId, request, ct);
        if (!result.IsSuccess) return this.ToActionResult(result.Error!);
        return AcceptedAtRoute("GetExport", new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Lists the most recent exports of a work (newest first).</summary>
    [HttpGet("api/works/{workId:guid}/exports", Name = "ListExports")]
    [ProducesResponseType<IReadOnlyList<ExportJobResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ExportJobResponse>>> List(Guid workId, CancellationToken ct)
    {
        var result = await exports.ListAsync(workId, ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToActionResult(result.Error!);
    }

    /// <summary>Status of an export job.</summary>
    [HttpGet("api/exports/{id:guid}", Name = "GetExport")]
    [ProducesResponseType<ExportJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ExportJobResponse>> Get(Guid id, CancellationToken ct)
    {
        var result = await exports.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToActionResult(result.Error!);
    }

    /// <summary>Downloads the generated file.</summary>
    /// <remarks>409 while the job is not finished (or failed); 410 after the file expired.</remarks>
    [HttpGet("api/exports/{id:guid}/file", Name = "DownloadExport")]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    public async Task<ActionResult> Download(Guid id, CancellationToken ct)
    {
        var result = await exports.OpenFileAsync(id, ct);
        if (!result.IsSuccess) return this.ToActionResult(result.Error!);
        var file = result.Value!;
        return File(file.Content, file.ContentType, file.FileName);
    }
}
