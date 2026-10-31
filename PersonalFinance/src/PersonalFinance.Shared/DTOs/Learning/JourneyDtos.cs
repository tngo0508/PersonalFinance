using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.Learning;

/// <summary>
/// An Account's Journey (GLOSSARY.md): every Published Lesson with its status, and the Dashboard preference. Titles
/// are in the Language asked for.
/// </summary>
public sealed class JourneyStateDto
{
    /// <summary>At least one Lesson is finished.</summary>
    public bool Started { get; set; }

    /// <summary>Every Lesson is finished: the Journey has ended.</summary>
    public bool Finished { get; set; }

    /// <summary>Whether the Dashboard shows the quiet Atelier card while a Lesson is open.</summary>
    public bool ShowOnDashboard { get; set; }

    /// <summary>Every Lesson, by day.</summary>
    public List<LessonProgressDto> Lessons { get; set; } = [];
}

/// <summary>One Lesson's place on the road.</summary>
public sealed class LessonProgressDto
{
    /// <summary>The Lesson's permanent slug.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Its day on the Journey, from 1.</summary>
    public int Day { get; set; }

    /// <summary>Its title in the Language asked for.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Locked, open or finished.</summary>
    public LessonStatus Status { get; set; }

    /// <summary>When its Check was answered (UTC); null unless finished.</summary>
    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>
    /// When it opens (UTC): the local midnight after the previous Lesson's finish. Null when it is open or finished,
    /// or when the previous Lesson is still unfinished.
    /// </summary>
    public DateTime? OpensAtUtc { get; set; }

    /// <summary>The keepsake a finished Lesson places on the desk.</summary>
    public KeepsakeDto Keepsake { get; set; } = new();
}

/// <summary>The object a finished Lesson places on the desk.</summary>
public sealed class KeepsakeDto
{
    /// <summary>The emoji drawn on the desk.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Its name, for screen readers, in the Language asked for.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>What the client posts once every Check question of the open Lesson is answered.</summary>
public sealed class FinishLessonRequest
{
    /// <summary>
    /// The browser's IANA time zone (<c>Asia/Ho_Chi_Minh</c>), which dates the next local midnight. Missing or
    /// unknown means UTC.
    /// </summary>
    public string? TimeZone { get; set; }
}

/// <summary>Today's Review (GLOSSARY.md): up to three flash cards from earlier Lessons.</summary>
public sealed class ReviewDto
{
    /// <summary>The cards to show, in order; empty on Day 1 and after the Journey ends.</summary>
    public List<ReviewCardDto> Cards { get; set; } = [];
}

/// <summary>One flash card: the Term on the front, its one-liner on the back.</summary>
public sealed class ReviewCardDto
{
    /// <summary>The Term Entry's permanent slug; marking the card posts it back.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>The Term in the Language asked for.</summary>
    public string Term { get; set; } = string.Empty;

    /// <summary>
    /// The Term in English. A page in another Language shows it under the Term, tying the new word to the one on US
    /// forms; an English page shows nothing there.
    /// </summary>
    public string EnglishTerm { get; set; } = string.Empty;

    /// <summary>The one-liner on the back of the card.</summary>
    public string OneLine { get; set; } = string.Empty;

    /// <summary>The slug of the finished Lesson the Term was taught in.</summary>
    public string LessonSlug { get; set; } = string.Empty;
}

/// <summary>What the client posts when a turned card is marked.</summary>
public sealed class MarkReviewCardRequest
{
    /// <summary><c>true</c> for "Knew it", <c>false</c> for "Not yet".</summary>
    public bool KnewIt { get; set; }
}

/// <summary>Whether the Dashboard shows the quiet Atelier card.</summary>
public sealed class ShowOnDashboardRequest
{
    /// <summary><c>true</c> shows the card, <c>false</c> hides it.</summary>
    public bool Show { get; set; }
}
