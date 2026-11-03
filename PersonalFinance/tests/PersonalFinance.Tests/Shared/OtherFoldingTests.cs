using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.Tests.Shared;

/// <summary>
/// The shared Other rule (Money Flow, Financial Universe): under 3% of the total flow folds, the sixth category folds,
/// and the kept ones take colour slots in display order.
/// </summary>
public class OtherFoldingTests
{
    [Fact]
    public void Fold_KeepsCategoriesAtOrAboveThreePercent_AndFoldsTheRest()
    {
        (string Name, decimal Amount)[] categories = [("Rent", 500m), ("Exactly", 30m), ("Just under", 29.99m)];

        OtherFoldingResult<(string Name, decimal Amount)> result = OtherFolding.Fold(categories, c => c.Amount, 1_000m);

        Assert.Equal(["Rent", "Exactly"], result.Kept.Select(k => k.Category.Name));
        Assert.Equal([0, 1], result.Kept.Select(k => k.ColorSlot));
        Assert.Equal(["Just under"], result.Folded.Select(f => f.Name));
        Assert.Equal(29.99m, result.OtherTotal);
    }

    [Fact]
    public void Fold_FoldsEverythingPastTheFiveColourSlots_WhateverItsSize()
    {
        (string Name, decimal Amount)[] categories = Enumerable.Range(1, 7).Select(i => ($"C{i}", 100m)).ToArray();

        OtherFoldingResult<(string Name, decimal Amount)> result = OtherFolding.Fold(categories, c => c.Amount, 700m);

        Assert.Equal(OtherFolding.MaxColoredCategories, result.Kept.Count);
        Assert.Equal(["C6", "C7"], result.Folded.Select(f => f.Name));
        Assert.Equal(200m, result.OtherTotal);
    }

    [Fact]
    public void Fold_WithNothing_FoldsNothing()
    {
        OtherFoldingResult<decimal> result = OtherFolding.Fold<decimal>([], c => c, 0m);

        Assert.Empty(result.Kept);
        Assert.Empty(result.Folded);
        Assert.Equal(0m, result.OtherTotal);
    }

    [Fact]
    public void IsOther_MatchesTheMoneyFlowRule()
    {
        Assert.True(OtherFolding.IsOther(29m, 1_000m, 0));
        Assert.False(OtherFolding.IsOther(30m, 1_000m, 0));
        Assert.True(OtherFolding.IsOther(500m, 1_000m, OtherFolding.MaxColoredCategories));
        Assert.Equal(0.03m, OtherFolding.Threshold);
    }
}
