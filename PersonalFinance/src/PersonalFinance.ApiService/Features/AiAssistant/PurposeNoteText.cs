namespace PersonalFinance.ApiService.Features.AiAssistant;

/// <summary>
/// Keeps a model-written Purpose Note to about 120 words of plain text, whatever the model returned: the output-token
/// cap bounds the cost, this bounds what the page shows.
/// </summary>
internal static class PurposeNoteText
{
    /// <summary>The most words a Purpose Note shows.</summary>
    public const int MaxWords = 120;

    /// <summary>A trimmed note ends at its last whole sentence when that keeps at least this many words.</summary>
    private const int MinWordsAtSentenceEnd = 40;

    /// <summary>The longest note in characters, well inside the Walk's 2,000-character column.</summary>
    public const int MaxLength = 1200;

    /// <summary>
    /// The note as given (whitespace-trimmed) when it has at most <see cref="MaxWords"/> words; otherwise its first
    /// <see cref="MaxWords"/> words, cut back to the last whole sentence when one ends late enough, or ending in an
    /// ellipsis when none does.
    /// </summary>
    public static string Trim(string text)
    {
        string note = text.Trim();
        string[] words = note.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > MaxWords)
        {
            string[] kept = words[..MaxWords];
            int lastSentenceEnd = Array.FindLastIndex(kept, EndsSentence);
            note = lastSentenceEnd + 1 >= MinWordsAtSentenceEnd
                ? string.Join(' ', kept[..(lastSentenceEnd + 1)])
                : string.Join(' ', kept) + "…";
        }

        return note.Length > MaxLength ? string.Concat(note.AsSpan(0, MaxLength - 1), "…") : note;
    }

    private static bool EndsSentence(string word) =>
        word.TrimEnd('"', '\'', ')', '”', '’') is { Length: > 0 } bare && bare[^1] is '.' or '!' or '?';
}
