namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>One share class's expense ratio from a shareholder report's XBRL.</summary>
/// <param name="ClassId">The class id, e.g. <c>C000007808</c>, or null when the fact names no class.</param>
/// <param name="Percent">The expense ratio in percent (0.03 is 0.03%).</param>
/// <param name="PeriodEnd">The last day of the period it covers, or null when not given.</param>
internal sealed record ClassExpenseRatio(string? ClassId, decimal Percent, DateOnly? PeriodEnd);
