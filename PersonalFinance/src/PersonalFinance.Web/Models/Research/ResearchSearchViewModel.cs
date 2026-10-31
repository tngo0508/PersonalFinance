namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// One Research search box (<c>_ResearchSearch</c>): the compact one in the Research navigation, or Home's large one.
/// </summary>
/// <param name="InputId">The input's id; its listbox is <c>{InputId}-list</c>.</param>
/// <param name="Large">True for Home's large box (full placeholder, no <c>/</c> hint); false for the compact one.</param>
/// <param name="CssClass">Classes added to the box for where it sits, or null.</param>
public sealed record ResearchSearchViewModel(string InputId, bool Large, string? CssClass = null);
