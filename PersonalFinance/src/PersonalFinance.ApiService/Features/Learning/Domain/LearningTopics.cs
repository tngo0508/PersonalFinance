namespace PersonalFinance.ApiService.Features.Learning.Domain;

/// <summary>
/// The fixed set of Topics (GLOSSARY.md) a Term Entry belongs to, by slug. The Web names them through i18n keys.
/// </summary>
public static class LearningTopics
{
    public const string SavingBanking = "saving-banking";
    public const string BorrowingCredit = "borrowing-credit";
    public const string InvestingBasics = "investing-basics";
    public const string Trading = "trading";
    public const string Tax = "tax";
    public const string Retirement = "retirement";

    /// <summary>Every Topic, in the Library's shelf order.</summary>
    public static IReadOnlyList<string> All { get; } =
        [SavingBanking, BorrowingCredit, InvestingBasics, Trading, Tax, Retirement];
}
