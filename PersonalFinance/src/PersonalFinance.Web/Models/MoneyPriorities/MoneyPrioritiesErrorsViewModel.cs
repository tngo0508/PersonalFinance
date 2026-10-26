namespace PersonalFinance.Web.Models.MoneyPriorities;

/// <summary>
/// A rejected submit (400) as the page's JavaScript reads it (<c>{ errors, fieldErrors }</c>), every message already in
/// the request's Language.
/// </summary>
public sealed class MoneyPrioritiesErrorsViewModel
{
    /// <summary>Messages about the whole request, shown above the form.</summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Messages keyed by the request property they belong to (e.g. <c>MonthlyAmount</c>), in order, so the page can
    /// show each one under its field.
    /// </summary>
    public Dictionary<string, List<string>> FieldErrors { get; set; } = [];
}
