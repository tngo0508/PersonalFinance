namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// An SEC industry office: <see cref="Order"/> is SEC's office number, which orders the offices on the page.
/// </summary>
internal sealed record SectorsOffice(int Order, string Name);
