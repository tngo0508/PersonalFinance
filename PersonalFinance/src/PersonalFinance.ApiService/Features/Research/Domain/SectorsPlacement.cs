namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Where a filer sits in the sunburst: its office, and its SIC industry within that office.
/// </summary>
/// <param name="Office">The filer's SEC industry office.</param>
/// <param name="SicCode">The filer's SIC code, or null when it has none.</param>
/// <param name="Industry">The SIC industry's name for display.</param>
internal sealed record SectorsPlacement(SectorsOffice Office, string? SicCode, string Industry);
