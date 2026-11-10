using Application.Documents;
using Application.Works;
using DTOs.Works;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Academic works of the current user.</summary>
[ApiController]
[Route("api/works")]
public sealed class WorksController(WorkService works) : ControllerBase
{
    /// <summary>Lists the user's works, most recently edited first.</summary>
    [HttpGet(Name = "ListWorks")]
    [ProducesResponseType<IReadOnlyList<WorkSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkSummaryResponse>>> List(CancellationToken ct) =>
        Ok(await works.ListAsync(ct));

    /// <summary>Creates a work with an empty document for the chosen norm profile.</summary>
    [HttpPost(Name = "CreateWork")]
    [Consumes("application/json")]
    [ProducesResponseType<WorkResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<WorkResponse>> Create(CreateWorkRequest request, CancellationToken ct)
    {
        var result = await works.CreateAsync(request, ct);
        if (!result.IsSuccess) return this.ToActionResult(result.Error!);
        return CreatedAtRoute("GetWork", new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Gets a work including its document.</summary>
    [HttpGet("{id:guid}", Name = "GetWork")]
    [ProducesResponseType<WorkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<WorkResponse>> Get(Guid id, CancellationToken ct)
    {
        var result = await works.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToActionResult(result.Error!);
    }

    /// <summary>
    /// Replaces the document (autosave). The document is sanitized and validated; send the
    /// <c>version</c> you loaded. A stale version returns 409 with <c>currentVersion</c>.
    /// </summary>
    [HttpPut("{id:guid}/document", Name = "SaveWorkDocument")]
    [Consumes("application/json")]
    [RequestSizeLimit(DocumentLimits.MaxRequestBytes)]
    [ProducesResponseType<SaveDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<SaveDocumentResponse>> SaveDocument(Guid id, SaveDocumentRequest request, CancellationToken ct)
    {
        var result = await works.SaveDocumentAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToActionResult(result.Error!);
    }

    /// <summary>Deletes a work.</summary>
    [HttpDelete("{id:guid}", Name = "DeleteWork")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await works.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : this.ToActionResult(result.Error!);
    }
}
