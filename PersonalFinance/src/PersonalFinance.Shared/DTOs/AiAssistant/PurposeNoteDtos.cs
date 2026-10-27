using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.AiAssistant;

/// <summary>One fact handed to the AI for a Purpose Note, already worded for display (e.g. "Debt balance", "$5,000").</summary>
public class PurposeNoteFactDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Everything the AI may see when writing a Purpose Note: the Purpose, the answers, the path's labels, the leaf and the
/// computed figures. Nothing else about the Account is sent. Dollar values arrive rounded to whole dollars.
/// </summary>
public class PurposeNoteRequestDto
{
    /// <summary>What the user wants their money to do, in their own words.</summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>The answers on the Walk's path.</summary>
    public List<PurposeNoteFactDto> Answers { get; set; } = [];

    /// <summary>The "Your path" sentences, in path order.</summary>
    public List<string> PathLabels { get; set; } = [];

    /// <summary>The suggested priority (the leaf's label).</summary>
    public string LeafLabel { get; set; } = string.Empty;

    /// <summary>The leaf's computed figures.</summary>
    public List<PurposeNoteFactDto> Figures { get; set; } = [];
}

/// <summary>The AI Assistant's answer to a <see cref="PurposeNoteRequestDto"/>.</summary>
public class PurposeNoteResponseDto
{
    /// <summary><see cref="PurposeNoteState.Written"/> with <see cref="Note"/>, or why there is no note.</summary>
    public PurposeNoteState State { get; set; }

    /// <summary>The note, plain text of about 120 words at most; null unless written.</summary>
    public string? Note { get; set; }

    /// <summary>What to show instead of a note; null when written.</summary>
    public string? Message { get; set; }

    /// <summary>
    /// Why the quota refused the note, keyed (ADR 0017): the daily limit or the monthly budget. Set only when
    /// <see cref="State"/> is <see cref="PurposeNoteState.QuotaReached"/>.
    /// </summary>
    public ApiErrorEntryDto? Reason { get; set; }

    /// <summary>The AI disclaimer shown under the note.</summary>
    public string Disclaimer { get; set; } = string.Empty;
}
