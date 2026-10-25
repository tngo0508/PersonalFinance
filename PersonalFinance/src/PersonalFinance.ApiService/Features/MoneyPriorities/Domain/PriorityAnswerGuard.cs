namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>The one way the Domain reports an answer the path or a leaf needs but was not given.</summary>
internal static class PriorityAnswerGuard
{
    /// <summary>
    /// <paramref name="answer"/>, or an <see cref="ArgumentException"/> "<paramref name="asker"/> <paramref name="name"/>,
    /// but it has no answer." when it is null.
    /// </summary>
    public static T Required<T>(T? answer, string name, string asker)
        where T : struct =>
        answer ?? throw Missing(name, asker);

    /// <summary>The exception for a missing answer. No paramName: the answer is a property, not a parameter (CA2208).</summary>
    public static ArgumentException Missing(string name, string asker) =>
        new($"{asker} {name}, but it has no answer.");
}
