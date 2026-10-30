using System.ComponentModel.DataAnnotations;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Community;

namespace PersonalFinance.Web.Models.Admin;

/// <summary>
/// View model for the Admin Poll list: every Poll, open and closed, and the form for writing a new one.
/// </summary>
public class AdminPollListViewModel
{
    /// <summary>
    /// Every Poll, newest first. Empty means there are none (a failed load shows the Unavailable State).
    /// </summary>
    public List<CommunityPollAdminItemDto> Items { get; set; } = new();

    /// <summary>
    /// The new Poll form, kept as typed when it is shown again with an error.
    /// </summary>
    public AdminPollForm Form { get; set; } = new();
}

/// <summary>
/// An Admin's new Poll as typed. Blank option fields are ignored; the ApiService has the final say on every rule.
/// </summary>
public class AdminPollForm
{
    [Required(ErrorMessage = "A question is required.")]
    [StringLength(CommunityPollLimits.QuestionMaxLength)]
    public string Question { get; set; } = string.Empty;

    [StringLength(CommunityPollLimits.DescriptionMaxLength)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose a category.")]
    public string Category { get; set; } = string.Empty;

    /// <summary>One field per possible option; blank ones are left out.</summary>
    public List<string?> Options { get; set; } = Enumerable.Repeat<string?>(null, CommunityPollLimits.MaxOptions).ToList();

    /// <summary>The options typed, trimmed, in order.</summary>
    public List<string> FilledOptions() =>
        Options.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o!.Trim()).ToList();
}
