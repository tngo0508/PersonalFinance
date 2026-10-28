namespace PersonalFinance.ApiService.Features.Learning.Domain;

/// <summary>
/// A Published Lesson of the Journey (GLOSSARY.md) in one Language, from its JSON file in the content folder
/// (ADR 0019). Text fields fall back to English one by one where the Vietnamese file leaves them out.
/// </summary>
public sealed record Lesson
{
    /// <summary>The permanent slug, also the file name (<c>borrow-wisely</c>).</summary>
    public required string Slug { get; init; }

    /// <summary>The Lesson's day on the Journey, contiguous from 1.</summary>
    public required int Day { get; init; }

    /// <summary>The title ("Borrow wisely").</summary>
    public required string Title { get; init; }

    /// <summary>A 2–3 sentence intro.</summary>
    public required string Intro { get; init; }

    /// <summary>The slugs of its 2–3 Term Entries, in teaching order.</summary>
    public required IReadOnlyList<string> Terms { get; init; }

    /// <summary>The Check: 2–3 questions.</summary>
    public required IReadOnlyList<CheckQuestion> Check { get; init; }

    /// <summary>The Try it link into the app.</summary>
    public required TryItLink TryIt { get; init; }

    /// <summary>The keepsake placed on the desk when the Lesson is finished.</summary>
    public required Keepsake Keepsake { get; init; }
}

/// <summary>One question of a Lesson's Check.</summary>
public sealed record CheckQuestion
{
    /// <summary><see cref="CheckFormats.MythOrFact"/> or <see cref="CheckFormats.WhatHappensNext"/>.</summary>
    public required string Format { get; init; }

    /// <summary>The prompt: 1–2 sentences.</summary>
    public required string Prompt { get; init; }

    /// <summary>The options: 2 for Myth or fact, 3 for What happens next, each at most 8 words.</summary>
    public required IReadOnlyList<string> Options { get; init; }

    /// <summary>The index of the correct option.</summary>
    public required int CorrectOption { get; init; }

    /// <summary>Shown after the answer: at most 2 sentences.</summary>
    public required string Explanation { get; init; }
}

/// <summary>The two formats of a Check question.</summary>
public static class CheckFormats
{
    /// <summary>"Myth or fact": two options.</summary>
    public const string MythOrFact = "myth-or-fact";

    /// <summary>"What happens next": three options.</summary>
    public const string WhatHappensNext = "what-happens-next";
}

/// <summary>A Lesson's Try it link to the app feature that puts the idea to work.</summary>
public sealed record TryItLink
{
    /// <summary>The feature's name ("Budget").</summary>
    public required string Label { get; init; }

    /// <summary>The app-relative path ("/Budget").</summary>
    public required string Url { get; init; }
}

/// <summary>The object a finished Lesson places on the desk.</summary>
public sealed record Keepsake
{
    /// <summary>The emoji drawn on the desk.</summary>
    public required string Symbol { get; init; }

    /// <summary>Its name, for screen readers ("Ledger").</summary>
    public required string Name { get; init; }
}
