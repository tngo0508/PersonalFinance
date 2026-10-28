using PersonalFinance.ApiService.Features.Learning.Domain;

namespace PersonalFinance.ApiService.Features.Learning;

/// <summary>
/// The Published Atelier content (ADR 0019), loaded from the content folder at startup and served from memory. Each
/// lookup takes a Language code (<c>en</c>, <c>vi</c>); an unknown one means English, and a field the Language's file
/// leaves out falls back to English.
/// </summary>
public interface ILearningContent
{
    /// <summary>Every Term Entry, by Term.</summary>
    IReadOnlyList<TermEntry> GetTerms(string language);

    /// <summary>The Term Entries of one Topic (<see cref="LearningTopics"/>), by Term; empty for an unknown Topic.</summary>
    IReadOnlyList<TermEntry> GetTermsInTopic(string topic, string language);

    /// <summary>The Term Entry with this slug, or <c>null</c>.</summary>
    TermEntry? FindTerm(string slug, string language);

    /// <summary>Every Lesson, by day.</summary>
    IReadOnlyList<Lesson> GetLessons(string language);

    /// <summary>The Lesson with this slug, or <c>null</c>.</summary>
    Lesson? FindLesson(string slug, string language);
}
