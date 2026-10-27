namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// Makes Pseudonyms (GLOSSARY.md) from two word lists, "Adjective Noun" (e.g. "Steady Heron"). The words are calm and
/// warm on purpose: a Pseudonym is how an Account is known in the Guild Hall, and nothing in it should flex, judge or
/// identify (ADR 0018). The lists are English in every Language; a Pseudonym is a name, not chrome.
/// </summary>
internal static class PseudonymGenerator
{
    /// <summary>Random draws before falling back to a scan: 32 misses on a list this size means it is nearly full.</summary>
    private const int RandomAttempts = 32;

    internal static readonly string[] Adjectives =
    [
        "Amber", "Bold", "Brave", "Breezy", "Bright", "Calm", "Candid", "Cheerful", "Clever", "Cordial",
        "Curious", "Daring", "Eager", "Earnest", "Fair", "Gallant", "Gentle", "Golden", "Graceful", "Hardy",
        "Hearty", "Honest", "Hopeful", "Humble", "Jolly", "Keen", "Kind", "Lively", "Loyal", "Lucky",
        "Mellow", "Merry", "Mindful", "Modest", "Nimble", "Noble", "Patient", "Peaceful", "Plucky", "Quiet",
        "Ready", "Rosy", "Rustic", "Sage", "Serene", "Sincere", "Spry", "Steady", "Sturdy", "Sunny",
        "Tender", "Thrifty", "Tidy", "Trusty", "Upbeat", "Vivid", "Warm", "Wise", "Witty", "Zesty",
    ];

    internal static readonly string[] Nouns =
    [
        "Wren", "Heron", "Kestrel", "Fox", "Willow", "Cedar", "Otter", "Finch", "Robin", "Sparrow",
        "Lark", "Owl", "Falcon", "Hawk", "Crane", "Egret", "Plover", "Puffin", "Linnet", "Starling",
        "Thrush", "Badger", "Beaver", "Hare", "Deer", "Elk", "Lynx", "Marten", "Vole", "Hedgehog",
        "Squirrel", "Seal", "Dolphin", "Oak", "Elm", "Birch", "Maple", "Aspen", "Alder", "Hazel",
        "Rowan", "Juniper", "Pine", "Spruce", "Fir", "Larch", "Yew", "Beech", "Poplar", "Reed",
        "Fern", "Moss", "Clover", "Heather", "Bramble", "Thistle", "Meadow", "Brook", "Pebble", "Lantern",
        "Ember", "Hearth", "Anvil", "Loom",
    ];

    /// <summary>How many different Pseudonyms the lists can make.</summary>
    internal static int Combinations => Adjectives.Length * Nouns.Length;

    /// <summary>Every Pseudonym the lists can make, adjective-major.</summary>
    internal static IEnumerable<string> AllNames() =>
        Adjectives.SelectMany(_ => Nouns, (adjective, noun) => $"{adjective} {noun}");

    /// <summary>
    /// A name not in <paramref name="taken"/>: a few random draws, then a scan of every combination from a random
    /// start, so a nearly full list still yields its free names. Once every combination is taken, a random one with
    /// the lowest free number after it ("Steady Heron 2"), so there is always a name.
    /// </summary>
    public static string Pick(IReadOnlySet<string> taken, Random random)
    {
        ArgumentNullException.ThrowIfNull(taken);
        ArgumentNullException.ThrowIfNull(random);

        for (int attempt = 0; attempt < RandomAttempts; attempt++)
        {
            string candidate = Compose(random.Next(Adjectives.Length), random.Next(Nouns.Length));
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }

        int start = random.Next(Combinations);
        for (int offset = 0; offset < Combinations; offset++)
        {
            int index = (start + offset) % Combinations;
            string candidate = Compose(index / Nouns.Length, index % Nouns.Length);
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }

        string baseName = Compose(random.Next(Adjectives.Length), random.Next(Nouns.Length));
        for (int number = 2; ; number++)
        {
            string candidate = $"{baseName} {number}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static string Compose(int adjective, int noun) => $"{Adjectives[adjective]} {Nouns[noun]}";
}
