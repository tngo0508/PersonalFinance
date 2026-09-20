using System.ComponentModel.DataAnnotations;

namespace PersonalFinance.Shared.DTOs;

public class ItemDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Item name is required.")]
    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}