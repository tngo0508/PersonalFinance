namespace PersonalFinance.Web.Models.Learning;

/// <summary>The i18n key that names each Topic, by its slug from ApiService.</summary>
public static class LearningTopicNames
{
    private static readonly Dictionary<string, string> Keys = new(StringComparer.Ordinal)
    {
        ["saving-banking"] = "Learning.Topic.SavingBanking",
        ["borrowing-credit"] = "Learning.Topic.BorrowingCredit",
        ["investing-basics"] = "Learning.Topic.InvestingBasics",
        ["trading"] = "Learning.Topic.Trading",
        ["tax"] = "Learning.Topic.Tax",
        ["retirement"] = "Learning.Topic.Retirement",
    };

    /// <summary>The Topic's i18n key; an unknown slug gets the generic "Other" key.</summary>
    public static string KeyFor(string slug) => Keys.GetValueOrDefault(slug, "Learning.Topic.Other");
}
