using System.Globalization;
using System.Xml;
using PersonalFinance.ApiService.Features.Research.Domain;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// Reads the expense ratios from the XBRL instance of a fund's tailored shareholder report (Form N-CSR or N-CSRS): each
/// <c>oef:ExpenseRatioPct</c> fact (a fraction, 0.0003 for 0.03%) with its context's share class
/// (<c>xbrldi:explicitMember dimension="oef:ClassAxis"</c>, e.g. <c>vif:C000007808Member</c>) and period end. The
/// instance is read as a stream; elements are matched by local name.
/// </summary>
internal static class XbrlExpenseRatioParser
{
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        Async = true,
        IgnoreComments = true,
        IgnoreWhitespace = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
    };

    /// <summary>
    /// Each expense ratio in the instance, in percent, with its class (null when its context names none) and period
    /// end. Throws <see cref="XmlException"/> on a malformed instance.
    /// </summary>
    public static async Task<IReadOnlyList<ClassExpenseRatio>> ParseAsync(Stream instance, CancellationToken cancellationToken)
    {
        var contexts = new Dictionary<string, (string? ClassId, DateOnly? End)>(StringComparer.Ordinal);
        var facts = new List<(string ContextId, decimal Fraction)>();
        using var reader = XmlReader.Create(instance, ReaderSettings);
        string? contextId = null;
        string? classId = null;
        DateOnly? end = null;
        // Reading an element's content leaves the reader on the next node already, so the loop must not read again.
        bool advance = true;
        while (!advance || await reader.ReadAsync())
        {
            advance = true;
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.EOF)
            {
                break;
            }

            if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "context" && contextId is not null)
            {
                contexts[contextId] = (classId, end);
                contextId = null;
                continue;
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            switch (reader.LocalName)
            {
                case "context":
                    contextId = reader.GetAttribute("id");
                    classId = null;
                    end = null;
                    break;
                case "explicitMember" when contextId is not null
                                           && (reader.GetAttribute("dimension") ?? string.Empty).EndsWith(":ClassAxis", StringComparison.Ordinal):
                    classId = ClassIdOf(await reader.ReadElementContentAsStringAsync());
                    advance = false;
                    break;
                case "endDate" when contextId is not null:
                case "instant" when contextId is not null:
                    end = DateOnly.TryParseExact((await reader.ReadElementContentAsStringAsync()).Trim(), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
                        ? date
                        : null;
                    advance = false;
                    break;
                case "ExpenseRatioPct":
                    string? factContext = reader.GetAttribute("contextRef");
                    string value = await reader.ReadElementContentAsStringAsync();
                    advance = false;
                    if (factContext is not null
                        && decimal.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal fraction))
                    {
                        facts.Add((factContext, fraction));
                    }

                    break;
            }
        }

        return facts
            .Where(f => contexts.ContainsKey(f.ContextId))
            .Select(f => new ClassExpenseRatio(contexts[f.ContextId].ClassId,
                Math.Round(f.Fraction * 100m, 4, MidpointRounding.AwayFromZero), contexts[f.ContextId].End))
            .ToList();
    }

    /// <summary>The class id in a class member such as <c>vif:C000007808Member</c>, or null.</summary>
    private static string? ClassIdOf(string member)
    {
        string local = member.Trim();
        int colon = local.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            local = local[(colon + 1)..];
        }

        if (local.EndsWith("Member", StringComparison.Ordinal))
        {
            local = local[..^"Member".Length];
        }

        return local.Length > 1 && local[0] == 'C' && local[1..].All(char.IsDigit) ? local : null;
    }
}
