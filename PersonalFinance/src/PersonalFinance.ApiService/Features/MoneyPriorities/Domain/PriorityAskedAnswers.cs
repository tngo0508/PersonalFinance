namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>Which of the optional answers a path asks for; the rest are off the path.</summary>
internal sealed record PriorityAskedAnswers(
    bool DebtBalance,
    bool EmergencyFundMonths,
    bool MonthlyExpenses,
    bool HasFullEmployerMatch,
    bool EmployerMatchPercent,
    bool HorizonYears,
    bool AgeBand,
    bool AtContributionLimit)
{
    /// <summary><paramref name="answers"/> with every answer this set does not ask for cleared to null.</summary>
    public PriorityAnswers KeepOnly(PriorityAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        return answers with
        {
            DebtBalance = DebtBalance ? answers.DebtBalance : null,
            EmergencyFundMonths = EmergencyFundMonths ? answers.EmergencyFundMonths : null,
            MonthlyExpenses = MonthlyExpenses ? answers.MonthlyExpenses : null,
            HasFullEmployerMatch = HasFullEmployerMatch ? answers.HasFullEmployerMatch : null,
            EmployerMatchPercent = EmployerMatchPercent ? answers.EmployerMatchPercent : null,
            HorizonYears = HorizonYears ? answers.HorizonYears : null,
            AgeBand = AgeBand ? answers.AgeBand : null,
            AtContributionLimit = AtContributionLimit ? answers.AtContributionLimit : null
        };
    }
}
