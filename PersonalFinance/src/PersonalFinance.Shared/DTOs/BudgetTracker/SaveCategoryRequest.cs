using System.Text.Json.Serialization;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Request to create or update a budget category.
/// </summary>
public class SaveCategoryRequest
{
    /// <summary>Category name (1-100 characters).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is an Expense or Income category. Always a number on the wire (1, 2): the attribute overrides
    /// Refit's default string enums, which the ApiService cannot bind. A converter on the enum type would not, since a
    /// converter in the serializer options outranks it.
    /// </summary>
    [JsonConverter(typeof(JsonNumberEnumConverter<BudgetCategoryKind>))]
    public BudgetCategoryKind Kind { get; set; }

    /// <summary>Sort position within this category kind.</summary>
    public int SortOrder { get; set; }
}
