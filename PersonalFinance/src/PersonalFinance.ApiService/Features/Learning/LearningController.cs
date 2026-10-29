using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.DTOs.Learning;

namespace PersonalFinance.ApiService.Features.Learning;

/// <summary>
/// API controller for the Atelier's public content: the Library and its Term Entries. Anonymous, like the Deletion
/// Request cancel link (ADR 0005), because the Library is readable before signing in. Each action takes the caller's
/// Language code; an unknown or missing one means English.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/learning")]
[Produces("application/json")]
public class LearningController : ControllerBase
{
    private readonly ILearningLibraryService _library;

    public LearningController(ILearningLibraryService library)
    {
        _library = library;
    }

    /// <summary>The Library: every Topic in shelf order, each with its Term Entries.</summary>
    [HttpGet("library")]
    [ProducesResponseType(typeof(LearningLibraryDto), StatusCodes.Status200OK)]
    public ActionResult<LearningLibraryDto> GetLibrary([FromQuery] string? language) =>
        _library.GetLibrary(language);

    /// <summary>The Term Entry with this slug; an unknown slug gets 404.</summary>
    [HttpGet("terms/{slug}")]
    [ProducesResponseType(typeof(TermEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<TermEntryDto> GetTerm(string slug, [FromQuery] string? language) =>
        _library.FindTerm(slug, language) is { } entry ? entry : NotFound();
}
