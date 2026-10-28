using PersonalFinance.ApiService.Features.Learning.ContentFiles;

namespace PersonalFinance.Tests.Learning.ContentChecks;

/// <summary>
/// The content checks across files: every reference resolves, names and aliases are unique, each Term Entry is in at
/// most one Lesson, and the Lessons' days run 1, 2, 3, … with no gap.
/// </summary>
internal static class LearningContentReferences
{
    public static void Check(IReadOnlyList<ContentFile<TermEntryDocument>> terms,
        IReadOnlyList<ContentFile<LessonDocument>> lessons, Action<string, string, string> add)
    {
        HashSet<string> slugs = terms.Select(t => t.Slug).ToHashSet(StringComparer.Ordinal);
        CheckNames(terms, add);
        CheckRelatedTerms(terms, slugs, add);
        CheckLessonTerms(lessons, slugs, add);
        CheckDays(lessons, add);
    }

    // A Term or alias names one entry only, compared without case ("ETF" and "etf" clash).
    private static void CheckNames(IReadOnlyList<ContentFile<TermEntryDocument>> terms, Action<string, string, string> add)
    {
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (ContentFile<TermEntryDocument> file in terms)
        {
            IEnumerable<(string Field, string? Name)> names =
            [
                ("term", file.Document.Term),
                .. (file.Document.AlsoKnownAs ?? []).Select((a, i) => ($"alsoKnownAs[{i}]", (string?)a)),
            ];
            foreach ((string field, string? name) in names.Where(n => !string.IsNullOrWhiteSpace(n.Name)))
            {
                string key = name!.Trim();
                if (owners.TryGetValue(key, out string? owner) && owner != file.Slug)
                {
                    add(file.Path, field, $"\"{key}\" already names the entry {owner}");
                }
                else
                {
                    owners[key] = file.Slug;
                }
            }
        }
    }

    private static void CheckRelatedTerms(IReadOnlyList<ContentFile<TermEntryDocument>> terms, HashSet<string> slugs,
        Action<string, string, string> add)
    {
        foreach (ContentFile<TermEntryDocument> file in terms)
        {
            List<string> related = file.Document.RelatedTerms ?? [];
            for (int i = 0; i < related.Count; i++)
            {
                if (related[i] == file.Slug)
                {
                    add(file.Path, $"relatedTerms[{i}]", "an entry is not related to itself");
                }
                else if (!slugs.Contains(related[i]))
                {
                    add(file.Path, $"relatedTerms[{i}]", $"no Term Entry has the slug \"{related[i]}\"");
                }
            }
        }
    }

    private static void CheckLessonTerms(IReadOnlyList<ContentFile<LessonDocument>> lessons, HashSet<string> slugs,
        Action<string, string, string> add)
    {
        // By day, so the later Lesson is the one told its Term Entry is taken.
        var taught = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ContentFile<LessonDocument> file in lessons.OrderBy(l => l.Document.Day ?? int.MaxValue))
        {
            List<string> terms = file.Document.Terms ?? [];
            for (int i = 0; i < terms.Count; i++)
            {
                if (!slugs.Contains(terms[i]))
                {
                    add(file.Path, $"terms[{i}]", $"no Term Entry has the slug \"{terms[i]}\"");
                }
                else if (!taught.TryAdd(terms[i], file.Slug))
                {
                    add(file.Path, $"terms[{i}]", $"\"{terms[i]}\" is already in the Lesson {taught[terms[i]]}");
                }
            }
        }
    }

    private static void CheckDays(IReadOnlyList<ContentFile<LessonDocument>> lessons, Action<string, string, string> add)
    {
        List<IGrouping<int, ContentFile<LessonDocument>>> days = lessons.Where(l => l.Document.Day is not null)
            .GroupBy(l => l.Document.Day!.Value)
            .OrderBy(g => g.Key)
            .ToList();
        for (int i = 0; i < days.Count; i++)
        {
            foreach (ContentFile<LessonDocument> lesson in days[i])
            {
                if (days[i].Count() > 1)
                {
                    add(lesson.Path, "day", $"{days[i].Key} is the day of {days[i].Count()} Lessons");
                }
                else if (days[i].Key != i + 1)
                {
                    add(lesson.Path, "day", $"is {days[i].Key}; the Lessons' days run 1, 2, 3, … with no gap");
                }
            }
        }
    }
}
