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
        var candidates = lines.Where(IsAffixCandidate).ToList();
        if (candidates.Count == 0)
        {
            warnings.Add("No affix lines found.");
            return [];
        }

        var affixes = new List<GearAffix>();
        foreach (var line in candidates)
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

    private static bool IsAffixCandidate(string line)
    {
        if (NoiseMarkers.Any(m => line.Contains(m, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return line.StartsWith('+') || line.Contains('%') || line.Any(char.IsDigit);
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
        var low = usable.Count < 3
            || item.Slot == GearSlot.Unknown
            || item.ItemPower is null
            || !item.Affixes.Any(a => a.IsResolved);

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
