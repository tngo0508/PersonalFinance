namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// One reported value of an XBRL concept in one unit.
/// </summary>
/// <param name="Start">The first day of a duration, or null for an instant.</param>
/// <param name="End">The last day of a duration, or the date of an instant.</param>
/// <param name="Value">The value as reported.</param>
/// <param name="FiscalYear">The fiscal year of the filing it was reported in, or null.</param>
/// <param name="FiscalPeriod">The fiscal period of that filing, e.g. <c>FY</c> or <c>Q2</c>, or null.</param>
/// <param name="Form">The form it was reported in, e.g. <c>10-K</c>.</param>
/// <param name="FiledOn">The date that filing was made.</param>
/// <param name="AccessionNumber">That filing's accession number.</param>
internal sealed record SecFact(
    DateOnly? Start,
    DateOnly End,
    decimal Value,
    int? FiscalYear,
    string? FiscalPeriod,
    string Form,
    DateOnly FiledOn,
    string AccessionNumber);
