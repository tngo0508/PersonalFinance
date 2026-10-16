namespace PersonalFinance.Data.Entities;

/// <summary>
/// Abstract base class for user-owned entities that provides audit fields and soft delete support.
/// All entities with a <c>UserId</c> field should inherit from this class.
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who owns this entity.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this entity was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when this entity was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this entity has been soft-deleted.
    /// When <c>true</c>, the entity is excluded from normal queries via EF Core global query filters.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this entity was soft-deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Marks this entity as soft-deleted by setting <see cref="IsDeleted"/> to <c>true</c>
    /// and <see cref="DeletedAt"/> to the current UTC time.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}
