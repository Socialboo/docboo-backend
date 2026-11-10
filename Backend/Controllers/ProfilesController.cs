using Domain.Profiles;
using DTOs.Works;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Formatting norm profiles available for new works.</summary>
[ApiController]
[Route("api/profiles")]
public sealed class ProfilesController(INormProfileCatalog catalog) : ControllerBase
{
    /// <summary>Lists the available norm profiles.</summary>
    [HttpGet(Name = "ListProfiles")]
    [ProducesResponseType<IReadOnlyList<ProfileResponse>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ProfileResponse>> List() =>
        Ok(catalog.List().Select(p => new ProfileResponse(p.Id, p.Name, p.Description, p.MaxSectionDepth)).ToList());
}
