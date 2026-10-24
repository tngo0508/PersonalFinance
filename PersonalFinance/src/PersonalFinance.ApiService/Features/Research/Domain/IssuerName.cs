using System.Text;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// A company name reduced to what matches it between a Form N-PORT holding and SEC's company list: upper case,
/// <c>&amp;</c> as AND, the state-of-incorporation suffix (<c>/DE/</c>, <c>/ MA</c>) dropped, punctuation dropped,
/// runs of single letters joined (<c>D R HORTON</c> is <c>DR HORTON</c>), and spellings of one legal form made one
/// (<c>COMPANY</c> is <c>CO</c>, <c>PUBLIC LTD CO</c> is <c>PLC</c>). <see cref="Base"/> is the name without its
/// trailing legal-form words, which are kept in <see cref="Forms"/>, so <c>Sanofi SA</c> and <c>Sanofi</c> share a base.
/// </summary>
/// <param name="Exact">Every word, joined, e.g. <c>WELLSFARGOANDCO</c>.</param>
/// <param name="Base">The words before the legal form, without a leading THE, joined, e.g. <c>WELLSFARGOAND</c>.</param>
/// <param name="Forms">The legal-form words the name ends in, e.g. <c>CO</c>; empty when it ends in none.</param>
/// <param name="Words">The words of <see cref="Base"/> in alphabetical order and joined by spaces, for SEC's
/// surname-first names (<c>SCHWAB CHARLES CORP</c> is <c>Charles Schwab Corp</c>).</param>
internal sealed record IssuerName(string Exact, string Base, IReadOnlySet<string> Forms, string Words)
{
    // Legal-form words a name can end in. NEW and DE (a re-formed company, Delaware) end a name but name no form.
    private static readonly HashSet<string> FormWords = new(StringComparer.Ordinal)
    {
        "INC", "CORP", "CO", "LTD", "PLC", "AG", "SA", "NV", "SE", "LLC", "LP", "SPA", "AB", "ASA", "OYJ", "KGAA", "SAB",
        "CV", "BV", "BHD", "TBK", "PCL", "AS",
    };

    private static readonly HashSet<string> NotForms = new(StringComparer.Ordinal) { "NEW", "DE", "THE" };

    private static readonly Dictionary<string, string> Synonyms = new(StringComparer.Ordinal)
    {
        ["COMPANY"] = "CO",
        ["CORPORATION"] = "CORP",
        ["INCORPORATED"] = "INC",
        ["LIMITED"] = "LTD",
        ["COS"] = "COMPANIES",
    };

    /// <summary>The name of <paramref name="name"/> for matching.</summary>
    public static IssuerName Of(string name)
    {
        List<string> words = WordsOf(name);
        string exact = string.Concat(words);
        int start = words.Count > 0 && words[0] == "THE" ? 1 : 0;
        int end = words.Count;
        var forms = new HashSet<string>(StringComparer.Ordinal);
        while (end > start && (FormWords.Contains(words[end - 1]) || NotForms.Contains(words[end - 1])))
        {
            if (FormWords.Contains(words[end - 1]))
            {
                forms.Add(words[end - 1]);
            }

            end--;
        }

        List<string> baseWords = words.GetRange(start, end - start);
        return new IssuerName(exact, string.Concat(baseWords), forms,
            string.Join(' ', baseWords.Order(StringComparer.Ordinal)));
    }

    /// <summary>The name's words after the clean-up described on <see cref="IssuerName"/>.</summary>
    private static List<string> WordsOf(string name)
    {
        string upper = DropStateSuffix(name.ToUpperInvariant().Replace("&", " AND ", StringComparison.Ordinal));
        var cleaned = new StringBuilder(upper.Length);
        foreach (char c in upper)
        {
            if (c is '.' or ',' or '\'')
            {
                continue;
            }

            cleaned.Append(c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') ? c : ' ');
        }

        var words = new List<string>();
        var initials = new StringBuilder();
        foreach (string word in cleaned.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length == 1 && char.IsLetter(word[0]))
            {
                initials.Append(word);
                continue;
            }

            if (initials.Length > 0)
            {
                words.Add(initials.ToString());
                initials.Clear();
            }

            words.Add(Synonyms.GetValueOrDefault(word, word));
        }

        if (initials.Length > 0)
        {
            words.Add(initials.ToString());
        }

        // PUBLIC LTD CO and PUBLIC CO LTD are how SEC spells PLC.
        for (int i = 0; i + 2 < words.Count; i++)
        {
            if (words[i] == "PUBLIC" && ((words[i + 1] == "LTD" && words[i + 2] == "CO") || (words[i + 1] == "CO" && words[i + 2] == "LTD")))
            {
                words[i] = "PLC";
                words.RemoveRange(i + 1, 2);
            }
        }

        return words;
    }

    /// <summary>
    /// The name without a trailing state or form suffix after a slash or backslash of two or three letters
    /// (<c>APPLE INC /CA/</c>, <c>VERTEX PHARMACEUTICALS INC / MA</c>, <c>CHARLES SCHWAB CORP/THE</c>).
    /// </summary>
    private static string DropStateSuffix(string name)
    {
        string trimmed = name.TrimEnd().TrimEnd('/', '\\').TrimEnd();
        int slash = trimmed.LastIndexOfAny(['/', '\\']);
        if (slash <= 0)
        {
            return trimmed;
        }

        string suffix = trimmed[(slash + 1)..].Trim();
        return suffix.Length is >= 2 and <= 3 && suffix.All(c => c is >= 'A' and <= 'Z') ? trimmed[..slash] : trimmed;
    }
}
