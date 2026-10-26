namespace PersonalFinance.Shared.Enums;

/// <summary>
/// Where a Contribution (GLOSSARY.md) came from.
/// </summary>
public enum ContributionKind
{
    /// <summary>A deposit or withdrawal the user recorded. Editable and deletable.</summary>
    Regular = 0,

    /// <summary>A goal's starting amount, at most one per goal. Editable and deletable.</summary>
    Opening = 1,

    /// <summary>The difference recorded by a balance correction. Can only be deleted.</summary>
    Adjustment = 2
}
