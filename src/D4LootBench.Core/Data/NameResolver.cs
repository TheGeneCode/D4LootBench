namespace D4LootBench.Core.Data;

/// <summary>
/// Resolves human-readable names (as output by the LLM) to hash IDs using the live catalogs.
/// Falls back to case-insensitive partial matching for suggestions when exact lookup fails.
/// </summary>
public sealed class NameResolver(IFilterDataService data)
{
    public bool TryResolveAffix(string name, out uint hash, out IReadOnlyList<string> suggestions)
    {
        if (data.Affixes.TryGetByName(name, out var entry))
        {
            hash = entry.Hash; suggestions = []; return true;
        }

        // Prefix-insensitive exact tier: guide/OCR phrases carry no leading sign, but catalog display
        // names may ("+Movement Speed", "%Cooldown Reduction"). Match on the sign-stripped key so
        // "Movement Speed" resolves cleanly instead of being derailed by a longer affix that merely
        // contains the phrase (e.g. "Evade Grants Movement Speed"), which makes the fuzzy pass ambiguous.
        var key = NormalizeAffixKey(name);
        var keyed = data.Affixes.All.Where(a => NormalizeAffixKey(a.Name) == key).ToList();
        if (keyed.Count == 1)
        {
            hash = keyed[0].Hash; suggestions = []; return true;
        }

        var candidates = data.Affixes.All.Select(a => a.Name).ToList();
        if (TryFuzzyResolve(name, candidates, out var resolved))
        {
            data.Affixes.TryGetByName(resolved, out var fuzzy);
            hash = fuzzy.Hash; suggestions = []; return true;
        }
        hash = 0; suggestions = FindSuggestions(name, candidates); return false;
    }

    /// <summary>True when <paramref name="name"/> names a catalog affix (ignoring any sign prefix), or is
    /// a leading portion of one (e.g. the guide phrase "Vulnerable Damage" for catalog "Vulnerable Damage
    /// Multiplier"). Used to distinguish an affix stat phrase from an item/aspect name. The match is
    /// one-directional on purpose — a CATALOG name may contain the query, but the query containing a
    /// shorter affix word does NOT qualify, so an item name like "Girdle of Boundless Strength" (which
    /// merely contains the affix "Strength") is correctly rejected.</summary>
    /// <param name="name">The candidate phrase.</param>
    /// <returns><c>true</c> when the phrase names an affix; otherwise <c>false</c>.</returns>
    public bool IsKnownAffixPhrase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (data.Affixes.TryGetByName(name, out _))
        {
            return true;
        }

        var key = NormalizeAffixKey(name);
        return key.Length > 0
            && data.Affixes.All.Any(a => NormalizeAffixKey(a.Name).Contains(key, StringComparison.Ordinal));
    }

    // Strips a leading +/% sign and surrounding whitespace so display names compare regardless of the
    // sign prefix D4 uses on some affixes.
    private static string NormalizeAffixKey(string name)
    {
        var s = name.Trim();
        var i = 0;
        while (i < s.Length && (s[i] == '+' || s[i] == '%'))
        {
            i++;
        }

        return s[i..].Trim().ToLowerInvariant();
    }

    public bool TryResolveItemType(string name, out uint hash, out IReadOnlyList<string> suggestions)
    {
        if (data.ItemTypes.TryGetByName(name, out var entry))
        {
            hash = entry.Hash; suggestions = []; return true;
        }
        var candidates = data.ItemTypes.All.Select(t => t.Name).ToList();
        if (TryFuzzyResolve(name, candidates, out var resolved))
        {
            data.ItemTypes.TryGetByName(resolved, out var fuzzy);
            hash = fuzzy.Hash; suggestions = []; return true;
        }
        hash = 0; suggestions = FindSuggestions(name, candidates); return false;
    }

    public bool TryResolveUnique(string name, out uint hash, out IReadOnlyList<string> suggestions)
    {
        if (data.Uniques.TryGetByName(name, out var entry))
        {
            hash = entry.SnoId; suggestions = []; return true;
        }
        var candidates = data.Uniques.Released.Select(u => u.Name).ToList();
        if (TryFuzzyResolve(name, candidates, out var resolved))
        {
            data.Uniques.TryGetByName(resolved, out var fuzzy);
            hash = fuzzy.SnoId; suggestions = []; return true;
        }
        hash = 0; suggestions = FindSuggestions(name, candidates); return false;
    }

    public bool TryResolveTalismanSet(string name, out uint hash, out IReadOnlyList<string> suggestions)
    {
        var match = data.TalismanSets.All
            .FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            hash = match.Hash; suggestions = []; return true;
        }
        var candidates = data.TalismanSets.All.Select(s => s.Name).ToList();
        if (TryFuzzyResolve(name, candidates, out var resolved))
        {
            hash = data.TalismanSets.All.First(s => s.Name == resolved).Hash;
            suggestions = []; return true;
        }
        hash = 0; suggestions = FindSuggestions(name, candidates); return false;
    }

    public bool TryResolveTalismanItem(string name, out uint itemHash, out uint setHash,
        out IReadOnlyList<string> suggestions)
    {
        var allItems = data.TalismanSets.All.SelectMany(s => s.Items);
        var match = allItems.FirstOrDefault(
            i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            itemHash = match.Hash;
            setHash = data.TalismanSets.GetSetHashForItem(match.Hash);
            suggestions = [];
            return true;
        }
        itemHash = 0;
        setHash = 0;
        suggestions = FindSuggestions(name, allItems.Select(i => i.Name));
        return false;
    }

    // Returns true only when the fuzzy pass yields exactly one candidate — unambiguous auto-resolve.
    private static bool TryFuzzyResolve(string query, IEnumerable<string> candidates, out string resolved)
    {
        var matches = FuzzyMatch(query, candidates).Take(2).ToList();
        if (matches.Count == 1) { resolved = matches[0]; return true; }
        resolved = ""; return false;
    }

    private static IEnumerable<string> FuzzyMatch(string query, IEnumerable<string> candidates)
    {
        var lower = query.ToLowerInvariant();
        return candidates.Where(c =>
        {
            var cl = c.ToLowerInvariant();
            return cl.Contains(lower) || lower.Contains(cl);
        });
    }

    private static IReadOnlyList<string> FindSuggestions(string query, IEnumerable<string> candidates, int max = 5)
        => FuzzyMatch(query, candidates).Take(max).ToList();
}
