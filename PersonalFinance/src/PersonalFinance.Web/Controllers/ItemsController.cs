using Microsoft.AspNetCore.Mvc;
using Refit;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Shared.Exceptions;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller orchestrating client requests and communicating with ApiService via typed Refit client.
/// Demonstrates CancellationToken cancellation propagation and structured exception handling.
/// </summary>
public class ItemsController : Controller
{
    private readonly IItemsApi _itemsApi;
    private readonly ILogger<ItemsController> _logger;

    public ItemsController(IItemsApi itemsApi, ILogger<ItemsController> logger)
    {
        _itemsApi = itemsApi;
        _logger = logger;
    }

    /// <summary>
    /// Displays items dashboard with interactive DataTables, Chart.js, Select2, and Leaflet.js components.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching items via Refit API client...");
        try
        {
            var items = await _itemsApi.GetItemsAsync(cancellationToken);
            return View(items);
        }
        catch (Refit.ApiException apiEx)
        {
            // Handles HTTP error status responses from ApiService (e.g. 404, 500)
            _logger.LogError(apiEx, "ApiService returned HTTP {StatusCode}: {Message}", apiEx.StatusCode,
                apiEx.Message);
            ViewBag.ErrorMessage = $"Backend service returned error: {(int)apiEx.StatusCode} ({apiEx.StatusCode})";
            return View(Enumerable.Empty<ItemDto>());
        }
        catch (PersonalFinance.Shared.Exceptions.ApiException customEx)
        {
            // Handles custom API exception contract
            _logger.LogError(customEx, "API communication error: {Message}", customEx.Message);
            ViewBag.ErrorMessage = customEx.StatusCode.HasValue
                ? $"Backend service returned error: {customEx.StatusCode.Value}"
                : "Unable to communicate with the ApiService backend.";
            return View(Enumerable.Empty<ItemDto>());
        }
        catch (HttpRequestException httpEx)
        {
            // Handles network failure / unreachable backend (handled gracefully by resilience pipeline retries first)
            _logger.LogError(httpEx, "Unable to reach ApiService backend at configured endpoint.");
            ViewBag.ErrorMessage =
                "Unable to connect to the ApiService backend. Please verify that the API service is running.";
            return View(Enumerable.Empty<ItemDto>());
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Items fetch request was canceled by the client.");
            return View(Enumerable.Empty<ItemDto>());
        }
        catch (Exception ex)
        {
            // Handles any unexpected runtime exceptions (such as Polly resilience timeout / broken circuit)
            _logger.LogError(ex, "Unexpected error occurred while communicating with the backend.");
            ViewBag.ErrorMessage = "An unexpected error occurred while communicating with the backend.";
            return View(Enumerable.Empty<ItemDto>());
        }
    }

    /// <summary>
    /// Handles new item submission from dashboard modal form.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ItemDto model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var items = await TryGetItemsFallbackAsync(cancellationToken);
            return View("Index", items);
        }

        try
        {
            await _itemsApi.CreateItemAsync(model, cancellationToken);
            _logger.LogInformation("Item '{ItemName}' created successfully via Refit client.", model.Name);
            return RedirectToAction(nameof(Index));
        }
        catch (Refit.ApiException apiEx)
        {
            _logger.LogError(apiEx, "Backend rejected item creation with status {StatusCode}.", apiEx.StatusCode);
            ModelState.AddModelError(string.Empty, $"Backend error ({(int)apiEx.StatusCode}): Could not save item.");
            var items = await TryGetItemsFallbackAsync(cancellationToken);
            return View("Index", items);
        }
        catch (PersonalFinance.Shared.Exceptions.ApiException customEx)
        {
            _logger.LogError(customEx, "API communication error creating item: {Message}", customEx.Message);
            ModelState.AddModelError(string.Empty, "Could not save item due to an API service error.");
            var items = await TryGetItemsFallbackAsync(cancellationToken);
            return View("Index", items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create item via ApiService.");
            ModelState.AddModelError(string.Empty,
                "An unexpected error occurred while communicating with the backend.");
            var items = await TryGetItemsFallbackAsync(cancellationToken);
            return View("Index", items);
        }
    }

    private async Task<IEnumerable<ItemDto>> TryGetItemsFallbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _itemsApi.GetItemsAsync(cancellationToken);
        }
        catch
        {
            return Enumerable.Empty<ItemDto>();
        }
    }
}