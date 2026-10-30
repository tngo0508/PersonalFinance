namespace PersonalFinance.Shared.DTOs.Learning;

/// <summary>
/// One Lesson of the Journey's outline (GLOSSARY.md), which guests see too: its day, title and keepsake, in the
/// Language asked for.
/// </summary>
public sealed class LessonSummaryDto
{
    /// <summary>The Lesson's permanent slug (<c>borrow-wisely</c>).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Its day on the Journey, from 1.</summary>
    public int Day { get; set; }

    /// <summary>Its title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The keepsake it places on the desk.</summary>
    public KeepsakeDto Keepsake { get; set; } = new();
}

/// <summary>
/// A Published Lesson's content in one Language: the intro, one sheet per Term Entry, the Check, Try it and the
/// keepsake. Public like the Library; the Web shows it only to a signed-in Account whose Lesson is open or finished.
/// </summary>
public sealed class LessonDto
{
    /// <summary>The Lesson's permanent slug.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Its day on the Journey, from 1.</summary>
    public int Day { get; set; }

    /// <summary>Its title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>A 2–3 sentence intro.</summary>
    public string Intro { get; set; } = string.Empty;

    /// <summary>Its Term Entries, in teaching order.</summary>
    public List<TermEntryDto> Terms { get; set; } = [];

    /// <summary>The Check: 2–3 questions.</summary>
    public List<CheckQuestionDto> Check { get; set; } = [];

    /// <summary>The Try it link into the app.</summary>
    public TryItDto TryIt { get; set; } = new();

    /// <summary>The keepsake a finished Lesson places on the desk.</summary>
    public KeepsakeDto Keepsake { get; set; } = new();
}

/// <summary>One question of a Lesson's Check.</summary>
public sealed class CheckQuestionDto
{
    /// <summary><c>myth-or-fact</c> or <c>what-happens-next</c>.</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>The prompt: 1–2 sentences.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>The options, in order.</summary>
    public List<string> Options { get; set; } = [];

    /// <summary>The index of the correct option.</summary>
    public int CorrectOption { get; set; }

    /// <summary>Shown after the answer: at most 2 sentences.</summary>
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>A Lesson's Try it link to the app feature that puts the idea to work.</summary>
public sealed class TryItDto
{
    /// <summary>The feature's name ("Budget").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The app-relative path ("/Budget").</summary>
    public string Url { get; set; } = string.Empty;
}
