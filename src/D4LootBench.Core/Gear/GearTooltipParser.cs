using System.Text.RegularExpressions;
using D4LootBench.Core.Data;

namespace D4LootBench.Core.Gear;

/// <summary>
/// Turns OCR lines from a D4 tooltip into a structured <see cref="GearParseResult"/>.
/// English client only; assumes "Advanced Tooltip Information" is ON. Purely structural —
/// there is no per-word confidence from Windows OCR, so <see cref="GearParseConfidence"/> is a
/// heuristic derived from which fields could be recovered. Greater-affix markers are icons OCR
/// cannot read and are never inferred here — they are toggled in the review step.
/// </summary>
public sealed partial class GearTooltipParser(IFilterDataService data)
{
    private const int MaxAffixes = 6;
    private const double MatchRateThreshold = 0.5;   // < half recognized ⇒ likely a bad capture
    private const int MatchRateFloorSampleSize = 3;  // need ≥3 affix candidates for the rate to mean anything

    private static readonly string[] NoiseMarkers =
    [
        "Requires Level", "Sell Value", "Item Power", "Account Bound", "Sockets",
        "Empty Socket", "Durability", "Upgrade", "Sacred", "Ancestral",
    ];

    // Weapon / off-hand base-damage properties printed under "Damage Per Second", between the Item Power
    // line and the real affixes. They carry roll-range brackets and numbers just like affixes ("[2,789 -
    // 4,039] Damage per Hit"), so the affix-marker test alone would admit them and let the markerless
    // "Attacks per Second" line prematurely terminate the affix run — cutting off every affix below,
    // including the trailing "]%" multiplier. Excluded by phrase so the genuine affixes are still reached.
    private static readonly string[] BaseStatMarkers =
    [
        "Damage Per Second", "Damage per Hit", "Attacks per Second",
    ];

    private static readonly string[] WeaponKeywords =
    [
        "mace", "sword", "axe", "staff", "scythe", "dagger", "polearm", "wand", "bow", "crossbow",
        "flail", "glaive",
    ];

    private static readonly char[] AffixMarkers = ['+', '%', '['];

    private readonly NameResolver _resolver = new(data);

    /// <summary>Parse trimmed OCR lines into a gear item plus confidence and warnings.</summary>
    /// <param name="lines">OCR lines in reading order (blank lines are ignored).</param>
    /// <returns>The parsed item with structural confidence and any warnings.</returns>
    public GearParseResult Parse(IReadOnlyList<string> lines)
    {
        var usable = lines
            .Select(l => CorrectOcrGlyphs(l?.Trim() ?? string.Empty))
            .Where(l => l.Length > 0)
            .ToList();

        if (usable.Count == 0)
        {
            return new GearParseResult
            {
                Item = new GearItem(),
                Confidence = GearParseConfidence.Low,
                Warnings = ["No readable text found in image."],
            };
        }

        var warnings = new List<string>();

        var itemPower = ParseItemPower(usable);
        if (itemPower is null)
        {
            warnings.Add("Item power not found.");
        }

        var isAncestral = usable.Any(l => l.Contains("Ancestral", StringComparison.OrdinalIgnoreCase));
        var rarity = ParseRarity(usable);

        var (slot, itemTypeName) = ParseSlot(usable);
        if (slot == GearSlot.Unknown)
        {
            warnings.Add("Item slot could not be determined.");
        }

        var affixes = ParseAffixes(usable, warnings);

        var item = new GearItem
        {
            Slot = slot,
            ItemTypeName = itemTypeName,
            ItemPower = itemPower,
            Rarity = rarity,
            IsAncestral = isAncestral,
            Affixes = affixes,
        };

        var confidence = DetermineConfidence(usable, item, warnings);

        return new GearParseResult
        {
            Item = item,
            Confidence = confidence,
            Warnings = warnings,
        };
    }

    private static int? ParseItemPower(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            if (!line.Contains("Item Power", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = ItemPowerRegex().Match(line);
            if (match.Success && int.TryParse(match.Value, out var power))
            {
                return power;
            }
        }

        return null;
    }

    private static ItemRarity ParseRarity(IReadOnlyList<string> lines)
    {
        var text = string.Join(' ', lines);
        (string keyword, ItemRarity rarity)[] priority =
        [
            ("Mythic", ItemRarity.Mythic),
            ("Unique", ItemRarity.Unique),
            ("Legendary", ItemRarity.Legendary),
            ("Rare", ItemRarity.Rare),
            ("Magic", ItemRarity.Magic),
            ("Common", ItemRarity.Common),
        ];

        foreach (var (keyword, rarity) in priority)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return rarity;
            }
        }

        return ItemRarity.Unknown;
    }

    // Matches the first line that carries a known item-type name (case-insensitive Contains,
    // longest catalog name wins to prefer "Two-Handed Sword" over "Sword").
    private (GearSlot Slot, string? ItemTypeName) ParseSlot(IReadOnlyList<string> lines)
    {
        var typeNames = data.ItemTypes.All
            .Select(t => t.Name)
            .OrderByDescending(n => n.Length)
            .ToList();

        foreach (var line in lines)
        {
            foreach (var typeName in typeNames)
            {
                if (line.Contains(typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return (MapItemTypeToSlot(typeName), typeName);
                }
            }
        }

        // Fallback: newer weapon types (Glaive, Quarterstaff) aren't yet in the filterable item-type
        // catalog, so the catalog scan above misses them (Flail is catalogued and matched there directly).
        // Recognize the bare weapon word so the slot is still Weapon; the recovered word becomes the
        // concrete ItemTypeName used for weapon SlotKey keying.
        foreach (var line in lines)
        {
            foreach (var keyword in WeaponKeywords)
            {
                if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    var typeName = char.ToUpperInvariant(keyword[0]) + keyword[1..];
                    return (GearSlot.Weapon, typeName);
                }
            }
        }

        return (GearSlot.Unknown, null);
    }

    private static GearSlot MapItemTypeToSlot(string typeName)
    {
        var t = typeName.ToLowerInvariant();
        return t switch
        {
            "helm" => GearSlot.Helm,
            "chest armor" => GearSlot.ChestArmor,
            "gloves" => GearSlot.Gloves,
            "pants" => GearSlot.Pants,
            "boots" => GearSlot.Boots,
            "amulet" => GearSlot.Amulet,
            "ring" => GearSlot.Ring,
            "shield" or "focus" or "totem" => GearSlot.Offhand,
            _ when WeaponKeywords.Any(k => t.Contains(k, StringComparison.Ordinal)) => GearSlot.Weapon,
            _ => GearSlot.Unknown,
        };
    }

    private List<GearAffix> ParseAffixes(IReadOnlyList<string> lines, List<string> warnings)
    {
        var block = ExtractBasicAffixBlock(lines);
        if (block.Count == 0)
        {
            warnings.Add("No affix lines found.");
            return [];
        }

        var affixes = new List<GearAffix>();
        foreach (var line in block)
        {
            if (affixes.Count >= MaxAffixes)
            {
                warnings.Add($"More than {MaxAffixes} affix lines detected — extra lines ignored.");
                break;
            }

            var phrase = NormalizeAffixPhrase(line);
            if (phrase.Length > 0 && _resolver.TryResolveAffix(phrase, out var hash, out _))
            {
                affixes.Add(new GearAffix
                {
                    RawText = line,
                    ResolvedName = data.Affixes.GetDisplayName(hash),
                    AffixHash = hash,
                });
            }
            else
            {
                affixes.Add(new GearAffix { RawText = line });
                warnings.Add($"Affix not recognized: \"{line}\"");
            }
        }

        return affixes;
    }

    // The basic-affix block is the run of affix-shaped lines beginning at or after the Item Power line.
    // "Affix-shaped" keys off structure OCR can actually read (see IsAffixLine): base stats, the
    // legendary/unique power sentence, and everything after it (tempered affixes, gem/socket bonuses) are
    // excluded, because OCR cannot read the diamond / star / anvil bullet icons that separate them in-game.
    private static List<string> ExtractBasicAffixBlock(IReadOnlyList<string> lines)
    {
        var start = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("Item Power", StringComparison.OrdinalIgnoreCase))
            {
                start = i + 1;
                break;
            }
        }

        var block = new List<string>();
        var started = false;
        for (var i = start; i < lines.Count; i++)
        {
            var line = lines[i];

            // Re-join an OCR-wrapped roll range onto the affix it belongs to rather than treating it as a line.
            if (block.Count > 0 && IsRangeContinuation(block[^1], line))
            {
                block[^1] = $"{block[^1]} {line}";
                continue;
            }

            if (IsAffixLine(line))
            {
                block.Add(line);
                started = true;
            }
            else if (started)
            {
                // First non-affix line after the run ends the basic block — the legendary/unique power
                // sentence, a noise marker, or a base stat. Tempered affixes and gem bonuses sit past it and
                // are deliberately excluded.
                break;
            }
        }

        return block;
    }

    // A basic-affix line, keyed off what OCR can see: not a noise marker, not a full sentence (the
    // legendary/unique power reads as prose and ends with '.'), and carrying an affix roll marker — '+',
    // '%', or '[' — which base stats ("1,594 Armor", "700 Damage Per Second") never do. This admits affixes
    // that start with a digit ("8.0% Cooldown Reduction"), an 'x' multiplier, or a wrapped range head, none
    // of which the old '+'-led scan accepted. Resolvability is intentionally NOT required for membership, so
    // a real-but-uncatalogued affix ("Fortify Generation") is still captured and surfaced for review.
    private static bool IsAffixLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0
            || NoiseMarkers.Any(m => trimmed.Contains(m, StringComparison.OrdinalIgnoreCase))
            || BaseStatMarkers.Any(m => trimmed.Contains(m, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (trimmed.EndsWith('.'))
        {
            return false;
        }

        return trimmed.IndexOfAny(AffixMarkers) >= 0;
    }

    // True when this line is the wrapped roll range of the previous affix, covering both OCR wrap shapes:
    //  • Fully wrapped — the entire range moved to its own line, so it OPENS with '[' ("[26 - 50]%").
    //    A genuine D4 affix never begins with '[' (they lead with +/x/a digit/a stat name), so a
    //    '['-led line is unambiguously a range tail regardless of the previous line's bracket state.
    //  • Half wrapped — the previous line left an unclosed '[' ("[14 -") and this line is the bare
    //    numeric tail that closes it ("20]%").
    private static bool IsRangeContinuation(string previous, string line)
    {
        if (line.TrimStart().StartsWith('['))
        {
            return true;
        }

        return HasUnclosedBracket(previous) && IsContinuationTail(line);
    }

    private static bool HasUnclosedBracket(string line)
        => line.Count(c => c == '[') > line.Count(c => c == ']');

    // A wrapped range tail carries only digits and range punctuation ("259]", "- 8.0]%") and never opens a
    // new affix (no leading '+'), so it can be safely appended to the previous unclosed line.
    private static bool IsContinuationTail(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length > 0
            && !trimmed.StartsWith('+')
            && ContinuationTailRegex().IsMatch(trimmed);
    }

    // Repairs the two OCR glyph defects seen on real captures before any structural parsing runs, so
    // every downstream heuristic — and the RawText shown in review — sees clean text. Both substitutions
    // are deliberately anchored to keep false positives out of genuine affix words:
    //  • A '%' printed after a roll range ("50]%") is frequently misread as a slash pair ("50]0/0",
    //    "50]0/6"). Only a '%'-shaped token IMMEDIATELY following ']' is rewritten to '%'.
    //  • A stray '1' reads as a vertical bar ('|'). Vertical bars never legitimately appear on D4 gear
    //    tooltips, so every '|' is mapped to '1'.
    private static string CorrectOcrGlyphs(string line)
    {
        if (line.Length == 0)
        {
            return line;
        }

        var corrected = PercentAfterBracketRegex().Replace(line, "]%");
        return corrected.Replace('|', '1');
    }

    // Strip roll ranges, numbers, %, +/- and collapse whitespace to leave the affix phrase.
    private static string NormalizeAffixPhrase(string line)
    {
        var stripped = RangeRegex().Replace(line, " ");
        stripped = NumberRegex().Replace(stripped, " ");
        stripped = stripped.Replace("%", " ").Replace("+", " ").Replace("-", " ");
        var phrase = WhitespaceRegex().Replace(stripped, " ").Trim();

        // D4 prints damage multipliers with a leading "x" sign ("x36%"). After the numeric strip it
        // survives as a lone leading "x"/"×" token that derails resolution — remove it so the bare stat
        // name resolves. Only a standalone leading token is stripped, never an 'x' inside a word.
        if (phrase.Length > 1
            && (phrase[0] is 'x' or 'X' or '×')
            && phrase[1] == ' ')
        {
            phrase = phrase[2..];
        }

        return phrase;
    }

    private static GearParseConfidence DetermineConfidence(
        IReadOnlyList<string> usable, GearItem item, List<string> warnings)
    {
        var affixCount = item.Affixes.Count;
        var resolvedCount = item.Affixes.Count(a => a.IsResolved);

        // Aggregate capture-quality signal: on a real tooltip most affixes resolve. A block with
        // several candidates but few recognized means the pixels themselves are poor (HDR washout,
        // tiny font, cropped tooltip) — distinct from one genuinely-unknown affix.
        var poorMatchRate = affixCount >= MatchRateFloorSampleSize
            && (double)resolvedCount / affixCount < MatchRateThreshold;
        if (poorMatchRate)
        {
            warnings.Add(
                $"Only {resolvedCount} of {affixCount} affixes were recognized — the capture is likely "
                + "poor (HDR washout, small font, or a cropped tooltip). Re-screenshot at higher resolution "
                + "with Advanced Tooltip Information ON.");
        }

        var low = usable.Count < 3
            || item.Slot == GearSlot.Unknown
            || item.ItemPower is null
            || resolvedCount == 0
            || poorMatchRate;

        if (low)
        {
            warnings.Add("Low-confidence parse — review carefully.");
            return GearParseConfidence.Low;
        }

        return GearParseConfidence.High;
    }

    [GeneratedRegex(@"\d{2,4}")]
    private static partial Regex ItemPowerRegex();

    [GeneratedRegex(@"\[[^\]]*\]?")]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"^[\d\s.,\-\[\]%]+$")]
    private static partial Regex ContinuationTailRegex();

    [GeneratedRegex(@"\d+(\.\d+)?")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    // A ']' immediately followed by a '%'-shaped slash pair ("0/0", "0/6", "o/o", "O/0" …).
    [GeneratedRegex(@"\][0oO]/[0-9oO]")]
    private static partial Regex PercentAfterBracketRegex();
}
