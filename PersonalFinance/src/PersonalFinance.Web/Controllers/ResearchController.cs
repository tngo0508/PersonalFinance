using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Models.Research;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Research page: public market data, for information only. Reads each dataset from
/// ApiService through <see cref="IResearchApi"/>; a dataset that has never loaded (503) or cannot be read shows that
/// tab's Unavailable State.
/// </summary>
[Authorize]
public sealed class ResearchController : Controller
{
    private readonly IResearchApi _researchApi;
    private readonly ILogger<ResearchController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchController"/> class.
    /// </summary>
    /// <param name="researchApi">ApiService client for the Research datasets.</param>
    /// <param name="logger">Logger for this controller.</param>
    public ResearchController(IResearchApi researchApi, ILogger<ResearchController> logger)
    {
        _researchApi = researchApi;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Research page, opening on the Sectors tab at the latest year.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        SectorsSnapshotDto? sectors = await ReadAsync(_researchApi.GetSectorsAsync, "Sectors", cancellationToken);
        EtfsSnapshotDto? etfs = await ReadAsync(_researchApi.GetEtfsAsync, "ETFs", cancellationToken);
        WorldSnapshotDto? world = await ReadAsync(_researchApi.GetWorldAsync, "World", cancellationToken);
        return View(new ResearchViewModel(sectors, etfs, world));
    }

    /// <summary>
    /// Reads one dataset; null when it has never loaded (503) or ApiService cannot be reached, so only that tab shows
    /// the Unavailable State.
    /// </summary>
    private async Task<TSnapshot?> ReadAsync<TSnapshot>(Func<CancellationToken, Task<TSnapshot>> read, string dataset,
        CancellationToken cancellationToken) where TSnapshot : class
    {
        try
        {
            return await read(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not load the Research {Dataset} data", dataset);
            return null;
        }
    }
}
