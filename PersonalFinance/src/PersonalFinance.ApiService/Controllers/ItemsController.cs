using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// RESTful API controller providing CRUD operations for catalog items.
/// Demonstrates async EF Core operations, AsNoTracking for query performance, and CancellationToken propagation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ItemsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ItemsController> _logger;

    public ItemsController(AppDbContext context, ILogger<ItemsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all catalog items from the database.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ItemDto>>> GetItems(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all items from database.");

        // Use AsNoTracking() for read-only queries to eliminate EF Core change tracking overhead
        var items = await _context.Items
            .AsNoTracking()
            .OrderByDescending(i => i.Id)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(i => new ItemDto
        {
            Id = i.Id,
            Name = i.Name,
            Description = i.Description,
            IsCompleted = i.IsCompleted,
            CreatedAtUtc = i.CreatedAtUtc
        }));
    }

    /// <summary>
    /// Retrieves a specific item by its unique ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItemDto>> GetItem(int id, CancellationToken cancellationToken = default)
    {
        var item = await _context.Items.FindAsync([id], cancellationToken);
        if (item == null)
        {
            _logger.LogWarning("Item with ID {ItemId} not found.", id);
            return NotFound();
        }

        return Ok(new ItemDto
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            IsCompleted = item.IsCompleted,
            CreatedAtUtc = item.CreatedAtUtc
        });
    }

    /// <summary>
    /// Creates a new catalog item.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ItemDto>> CreateItem([FromBody] ItemDto dto,
        CancellationToken cancellationToken = default)
    {
        var item = new Item
        {
            Name = dto.Name,
            Description = dto.Description,
            IsCompleted = dto.IsCompleted,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new item with ID {ItemId}", item.Id);

        dto.Id = item.Id;
        dto.CreatedAtUtc = item.CreatedAtUtc;
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, dto);
    }
}