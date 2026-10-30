using System.ComponentModel.DataAnnotations;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.Admin;

/// <summary>
/// View model for the Admin Challenge list: every Challenge, and the form for writing a new one.
/// </summary>
public class AdminChallengeListViewModel
{
    /// <summary>
    /// Every Challenge, newest start first. Empty means there are none (a failed load shows the Unavailable State).
    /// </summary>
    public List<CommunityChallengeAdminItemDto> Items { get; set; } = new();

    /// <summary>
    /// The new Challenge form, kept as typed when it is shown again with an error.
    /// </summary>
    public AdminChallengeForm Form { get; set; } = new();
}

/// <summary>
/// An Admin's new Challenge as typed. The ApiService has the final say on every rule, the no-amounts one included.
/// </summary>
public class AdminChallengeForm
{
    [Required(ErrorMessage = "A title is required.")]
    [StringLength(CommunityChallengeLimits.TitleMaxLength)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "A habit is required.")]
    [StringLength(CommunityChallengeLimits.HabitMaxLength)]
    public string Habit { get; set; } = string.Empty;

    public CommunityChallengePeriod Period { get; set; } = CommunityChallengePeriod.Daily;

    [Required(ErrorMessage = "Choose a start date.")]
    public DateOnly? StartDate { get; set; }

    [Required(ErrorMessage = "Choose an end date.")]
    public DateOnly? EndDate { get; set; }
}
