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

    private static readonly string[] WeaponKeywords =
    [
        "mace", "sword", "axe", "staff", "scythe", "dagger", "polearm", "wand", "bow", "crossbow",
    ];

    private readonly NameResolver _resolver = new(data);

    /// <summary>Parse trimmed OCR lines into a gear item plus confidence and warnings.</summary>
    /// <param name="lines">OCR lines in reading order (blank lines are ignored).</param>
    /// <returns>The parsed item with structural confidence and any warnings.</returns>
    public GearParseResult Parse(IReadOnlyList<string> lines)
    {
        var usable = lines
            .Select(l => l?.Trim() ?? string.Empty)
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

    // The basic-affix block is the first contiguous run of "+"-led lines at or after the Item Power
    // line. This deliberately excludes the item header and base stats (Armor / DPS / Item Power — never
    // "+"-led), the legendary/unique power (a sentence that is not "+"-led, which ends the run), and
    // everything after it: tempered affixes and gem/socket bonuses. OCR cannot read the diamond / star /
    // anvil bullet icons that separate these in-game, so structural position is the only signal we have.
    private static List<string> ExtractBasicAffixBlock(IReadOnlyList<string> lines)
    {
        // Anchor the search after the Item Power line when present; base stats sit between it and the
        // affixes. With no Item Power line (a cropped tooltip) fall back to scanning from the top.
        var start = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("Item Power", StringComparison.OrdinalIgnoreCase))
            {
                start = i + 1;
                break;
            }
        }

        var first = -1;
        for (var i = start; i < lines.Count; i++)
        {
            if (IsAffixLine(lines[i]))
            {
                first = i;
                break;
            }
        }

        if (first < 0)
        {
            return [];
        }

        var block = new List<string>();
        for (var i = first; i < lines.Count && IsAffixLine(lines[i]); i++)
        {
            block.Add(lines[i]);
        }

        return block;
    }

    // A "+"-led line: after any leading icon/punctuation junk, the first meaningful glyph is '+'. D4
    // renders every gear affix as "+X …" (basic, greater, tempered, and gem lines all qualify); base
    // stats and power sentences never lead with '+'. Noise markers are excluded so a stray line such as
    // "+Sockets" cannot anchor or extend the affix block.
    private static bool IsAffixLine(string line)
    {
        if (NoiseMarkers.Any(m => line.Contains(m, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        foreach (var c in line)
        {
            if (c == '+')
            {
                return true;
            }

            if (char.IsLetterOrDigit(c))
            {
                return false;
            }
        }

        return false;
    }

    // Strip roll ranges, numbers, %, +/- and collapse whitespace to leave the affix phrase.
    private static string NormalizeAffixPhrase(string line)
    {
        var stripped = RangeRegex().Replace(line, " ");
        stripped = NumberRegex().Replace(stripped, " ");
        stripped = stripped.Replace("%", " ").Replace("+", " ").Replace("-", " ");
        return WhitespaceRegex().Replace(stripped, " ").Trim();
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

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"\d+(\.\d+)?")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
