namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>A fixed rate a balance is projected at; see <see cref="PriorityAssumptions"/>.</summary>
internal enum GrowthScenario
{
    /// <summary>High-yield savings or short-term Treasuries, 3.5%.</summary>
    Cash,

    /// <summary>Conservative investment return, 4%.</summary>
    Conservative,

    /// <summary>Base investment return, 7%.</summary>
    Base,

    /// <summary>Aggressive investment return, 10%.</summary>
    Aggressive
}
