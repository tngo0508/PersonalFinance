namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// One filing in a company's SEC submissions.
/// </summary>
/// <param name="AccessionNumber">The accession number, e.g. <c>0000950170-24-087843</c>.</param>
/// <param name="Form">The form type, e.g. <c>10-K</c>.</param>
/// <param name="FiledOn">The filing date.</param>
/// <param name="ReportedOn">The period reported on, or null.</param>
/// <param name="Items">For an 8-K, its item codes, e.g. <c>2.02</c>; otherwise empty.</param>
internal sealed record SecFilingEntry(
    string AccessionNumber,
    string Form,
    DateOnly FiledOn,
    DateOnly? ReportedOn,
    IReadOnlyList<string> Items);
