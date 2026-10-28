namespace PersonalFinance.Web.Models.CurrencyConverter;

/// <summary>
/// A live quote that failed (400 or 502) as the page's JavaScript reads it (<c>{ errors, fieldErrors }</c>), every
/// message already in the request's Language.
/// </summary>
public sealed class CurrencyConverterErrorsViewModel
{
    /// <summary>Messages about the whole request, shown above the card.</summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Messages keyed by the query parameter they belong to (<c>Amount</c>, <c>From</c>, <c>To</c>, <c>Date</c>), in
    /// order, so the page can show each one under its field.
    /// </summary>
    public Dictionary<string, List<string>> FieldErrors { get; set; } = [];
}
