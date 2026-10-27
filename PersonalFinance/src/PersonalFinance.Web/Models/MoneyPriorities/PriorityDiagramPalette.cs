namespace PersonalFinance.Web.Models.MoneyPriorities;

/// <summary>
/// The colours of a Priority Tree diagram, as <c>#RRGGBB</c> hex values. Mermaid bakes colours into the SVG it renders,
/// so a palette is a full set and a theme change means generating the text again with another palette. Light only for
/// now (spec #315); a dark palette can be added beside <see cref="Light"/>.
/// </summary>
/// <param name="Background">The page background behind the start node.</param>
/// <param name="Ink">Main text: questions, the start node and highlighted edge labels.</param>
/// <param name="SoftText">Leaf text before a submit.</param>
/// <param name="Line">Edges before a submit, and the start node's border.</param>
/// <param name="DimText">Text of nodes and edge labels off the path. Must keep at least 4.5:1 on white.</param>
/// <param name="DimLine">Edges off the path.</param>
/// <param name="QuestionFill">Question fill before a submit.</param>
/// <param name="QuestionStroke">Question border before a submit.</param>
/// <param name="LeafFill">Leaf fill before a submit.</param>
/// <param name="LeafStroke">Leaf border before a submit.</param>
/// <param name="OnPathFill">Fill of the path's nodes before the leaf.</param>
/// <param name="OnPathStroke">Border of the path's nodes, and the path's edges.</param>
/// <param name="ChosenFill">Fill of the leaf the path ends at.</param>
/// <param name="ChosenText">Text of the leaf the path ends at.</param>
/// <param name="ChosenStroke">Border of the leaf the path ends at.</param>
/// <param name="DimFill">Fill of nodes off the path.</param>
/// <param name="DimStroke">Border of nodes off the path.</param>
internal sealed record PriorityDiagramPalette(
    string Background,
    string Ink,
    string SoftText,
    string Line,
    string DimText,
    string DimLine,
    string QuestionFill,
    string QuestionStroke,
    string LeafFill,
    string LeafStroke,
    string OnPathFill,
    string OnPathStroke,
    string ChosenFill,
    string ChosenText,
    string ChosenStroke,
    string DimFill,
    string DimStroke)
{
    /// <summary>
    /// The light palette from the re-render prototype (#304), with dimmed text at <c>#5B6B78</c> (about 5.3:1 on
    /// white) instead of the prototype's <c>#8796A2</c> (about 3:1).
    /// </summary>
    public static PriorityDiagramPalette Light { get; } = new(
        Background: "#FFFFFF",
        Ink: "#14283B",
        SoftText: "#3D4F5F",
        Line: "#5B6B78",
        DimText: "#5B6B78",
        DimLine: "#D6DEDB",
        QuestionFill: "#FBF3DD",
        QuestionStroke: "#8A6A1E",
        LeafFill: "#F3F6F5",
        LeafStroke: "#D6DEDB",
        OnPathFill: "#E6F0EB",
        OnPathStroke: "#1D6A50",
        ChosenFill: "#1D6A50",
        ChosenText: "#FFFFFF",
        ChosenStroke: "#134A38",
        DimFill: "#F8FAF9",
        DimStroke: "#E1E7E4");

    /// <summary>Every colour, in declaration order, so the generator can check each is a hex value.</summary>
    internal IEnumerable<string> Colours =>
    [
        Background, Ink, SoftText, Line, DimText, DimLine, QuestionFill, QuestionStroke, LeafFill, LeafStroke,
        OnPathFill, OnPathStroke, ChosenFill, ChosenText, ChosenStroke, DimFill, DimStroke
    ];
}
