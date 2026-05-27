using System.Text.RegularExpressions;

namespace D4LootBench.Core.Import;

/// <summary>
/// Parses the gear section copied from a Maxroll build guide page.
/// Blocks are delimited by known slot-name keywords. Affixes are listed in priority order (implicit).
/// Supports ↑ (Greater Affix), x prefix (multiplicative), "Unique Effect" sentinel, Seal/Charm talisman slots.
/// </summary>
public sealed partial class MaxrollParser : IBuildGuideParser
{
    private static readonly HashSet<string> SlotKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Helm", "Chest Armor", "Gloves", "Pants", "Boots",
        "Amulet", "Left Ring", "Right Ring",
        "Mainhand", "Offhand",
        "Seal", "Weapon"
    };

    private static readonly HashSet<string> TalismanKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Seal"
    };

    private static readonly Regex CharmPattern = CharmRegex();

    private enum State { Idle, AfterSlotName, AffixList, UniqueBonus, Talisman }

    public ParsedBuildGuide Parse(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var slots = new List<ParsedSlot>();

        var state = State.Idle;
        string? slotLabel = null;
        string? itemName = null;
        var hasUniqueSentinel = false;
        var isTalisman = false;
        var affixes = new List<ParsedAffix>();
        var talismanEmitted = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            var isBlank = string.IsNullOrEmpty(line);
            if (isBlank) continue;

            var isSlotKeyword = IsSlotKeyword(line);

            switch (state)
            {
                case State.Idle:
                    if (isSlotKeyword)
                        BeginSlot(line);
                    break;

                case State.AfterSlotName:
                    itemName = line;
                    state = State.AffixList;
                    break;

                case State.AffixList:
                    if (isSlotKeyword)
                    {
                        EmitSlot();
                        BeginSlot(line);
                    }
                    else if (line.Equals("Unique Effect", StringComparison.OrdinalIgnoreCase))
                    {
                        hasUniqueSentinel = true;
                        state = State.UniqueBonus;
                    }
                    else
                    {
                        var (name, isGa) = StripAffixModifiers(line);
                        affixes.Add(new ParsedAffix { RawName = name, IsGreaterAffix = isGa, Priority = 0 });
                    }
                    break;

                case State.UniqueBonus:
                    // Discard unique bonus lines until next slot keyword
                    if (isSlotKeyword)
                    {
                        EmitSlot();
                        BeginSlot(line);
                    }
                    break;

                case State.Talisman:
                    // Discard charm lines; emit show-all talisman rule once on first encounter
                    if (!talismanEmitted)
                    {
                        slots.Add(new ParsedSlot
                        {
                            SlotLabel = "Seal",
                            IsTalismanSlot = true,
                            Affixes = []
                        });
                        talismanEmitted = true;
                    }
                    if (isSlotKeyword && !IsTalismanKeyword(line))
                    {
                        state = State.Idle;
                        BeginSlot(line);
                    }
                    break;

                default:
                    break;
            }
        }

        EmitSlot();

        return new ParsedBuildGuide { DetectedFormat = BuildGuideFormat.Maxroll, Slots = slots };

        void BeginSlot(string label)
        {
            if (IsTalismanKeyword(label))
            {
                state = State.Talisman;
                return;
            }
            slotLabel = label;
            itemName = null;
            hasUniqueSentinel = false;
            isTalisman = false;
            affixes.Clear();
            state = State.AfterSlotName;
        }

        void EmitSlot()
        {
            if (slotLabel is null) return;
            slots.Add(new ParsedSlot
            {
                SlotLabel = slotLabel,
                ItemName = itemName,
                HasUniqueSentinel = hasUniqueSentinel,
                IsTalismanSlot = isTalisman,
                Affixes = [.. affixes]
            });
            slotLabel = null;
            itemName = null;
            hasUniqueSentinel = false;
            isTalisman = false;
            affixes.Clear();
        }
    }

    private static bool IsSlotKeyword(string line)
        => SlotKeywords.Contains(line) || CharmPattern.IsMatch(line);

    private static bool IsTalismanKeyword(string line)
        => TalismanKeywords.Contains(line) || CharmPattern.IsMatch(line);

    /// <summary>Strips x prefix (multiplicative) and ↑ suffix (Greater Affix marker).</summary>
    private static (string Name, bool IsGa) StripAffixModifiers(string raw)
    {
        var name = raw.TrimStart();
        if (name.StartsWith('x') || name.StartsWith('X'))
            name = name[1..].TrimStart();
        var isGa = name.EndsWith('↑');
        if (isGa)
            name = name[..^1].TrimEnd();
        return (name, isGa);
    }

    [GeneratedRegex(@"^Charm\s*\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex CharmRegex();
}
