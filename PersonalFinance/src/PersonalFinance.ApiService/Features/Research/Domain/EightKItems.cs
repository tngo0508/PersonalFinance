namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// The items of SEC Form 8-K in plain words, shortened from the form's own item titles, so a Company Profile can say
/// what an 8-K reports without the SEC's numbering.
/// </summary>
internal static class EightKItems
{
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.Ordinal)
    {
        ["1.01"] = "Entry into a material agreement",
        ["1.02"] = "Termination of a material agreement",
        ["1.03"] = "Bankruptcy or receivership",
        ["1.04"] = "Mine safety: shutdown or pattern of violations",
        ["1.05"] = "Material cybersecurity incident",
        ["2.01"] = "Completion of acquisition or disposition",
        ["2.02"] = "Results of operations",
        ["2.03"] = "New financial obligation",
        ["2.04"] = "Acceleration of a financial obligation",
        ["2.05"] = "Costs of exit or disposal activities",
        ["2.06"] = "Material impairment",
        ["3.01"] = "Delisting or listing rule notice",
        ["3.02"] = "Unregistered sale of equity",
        ["3.03"] = "Change to shareholders' rights",
        ["4.01"] = "Change of auditor",
        ["4.02"] = "Earlier financial statements no longer reliable",
        ["5.01"] = "Change in control",
        ["5.02"] = "Departure or appointment of directors or officers",
        ["5.03"] = "Change to articles, bylaws or fiscal year",
        ["5.04"] = "Suspension of employee benefit plan trading",
        ["5.05"] = "Change to or waiver of the code of ethics",
        ["5.06"] = "Change in shell company status",
        ["5.07"] = "Shareholder vote results",
        ["5.08"] = "Shareholder director nominations",
        ["6.01"] = "Asset-backed securities: informational material",
        ["6.02"] = "Asset-backed securities: change of servicer or trustee",
        ["6.03"] = "Asset-backed securities: change in credit enhancement",
        ["6.04"] = "Asset-backed securities: failure to make a distribution",
        ["6.05"] = "Asset-backed securities: securities act updating disclosure",
        ["7.01"] = "Regulation FD disclosure",
        ["8.01"] = "Other events",
        ["9.01"] = "Financial statements and exhibits",
    };

    /// <summary>The plain words of an item code such as <c>2.02</c>; an unknown code is returned as it is.</summary>
    public static string Describe(string code) =>
        Descriptions.TryGetValue(code.Trim(), out string? description) ? description : code.Trim();
}
