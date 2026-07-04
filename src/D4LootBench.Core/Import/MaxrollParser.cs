using System.Text.RegularExpressions;
using D4LootBench.Core.Data;

namespace D4LootBench.Core.Import;

/// <summary>
/// Parses the gear section copied from a Maxroll build guide page.
/// Blocks are delimited by known slot-name keywords. Affixes are listed in priority order (implicit),
/// with the slot's tempered affix as the trailing positional line — that last affix is dropped (a drop
/// never pre-rolls a tempered affix), except when it is the slot's sole affix line.
/// Supports ↑ (Greater Affix), x prefix (multiplicative), "Unique Effect" sentinel, Seal/Charm talisman slots.
/// </summary>
/// <param name="affixResolver">Resolver used to tell a first-line item/aspect name from a first-line
/// affix (low-level guides omit the item name). When <c>null</c>, the first line after a slot header is
/// always taken as the item name (legacy behavior).</param>
public sealed partial class MaxrollParser(NameResolver? affixResolver = null) : IBuildGuideParser
{
    private static readonly HashSet<string> SlotKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Helm", "Chest Armor", "Gloves", "Pants", "Boots",
        "Amulet", "Left Ring", "Right Ring",
        "Mainhand", "Offhand",
        // Barbarian two-handed arsenal slots (Maxroll labels them by weapon expertise, not "Mainhand").
        // GoalBuildFactory.MapSlot routes these to the class-aware Bludgeoning/Slicing roles.
        "Bludgeoning Weapon", "Slicing Weapon",
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
                    // Usually the first line after a slot header is the item/aspect name, but low-level
                    // guides omit it and go straight to ranked affixes. Only treat the line as a name when
                    // it does NOT resolve as an affix; otherwise it is affix #1 and must not be swallowed.
                    if (LooksLikeAffix(line))
                    {
                        AddAffix(line);
                    }
                    else
                    {
                        itemName = line;
                    }
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
                        AddAffix(line);
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

        void AddAffix(string line)
        {
            var (name, isGa) = StripAffixModifiers(line);
            affixes.Add(new ParsedAffix { RawName = name, IsGreaterAffix = isGa, Priority = 0 });
        }

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

            // Maxroll lists the slot's tempered affix as the last positional line; drops never pre-roll
            // a tempered affix (the player tempers it in afterward), so exclude it from the target set —
            // mirroring the temper stripping the Icy Veins / Mobalytics parsers already do structurally.
            // Guard the sole-affix case so a one-line slot keeps its single target.
            if (affixes.Count > 1)
            {
                affixes.RemoveAt(affixes.Count - 1);
            }

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

    // A first line is affix #1 (rather than an item/aspect name) when it resolves against the affix
    // catalog. Uses the same resolver as downstream goal-building, so a line is kept as an affix exactly
    // when it would resolve there. With no resolver, defer to the legacy "first line is the item name".
    private bool LooksLikeAffix(string line)
    {
        if (affixResolver is null)
        {
            return false;
        }

        var (name, _) = StripAffixModifiers(line);
        return affixResolver.IsKnownAffixPhrase(name);
    }

    private static bool IsSlotKeyword(string line)
        => SlotKeywords.Contains(line) || CharmPattern.IsMatch(line);

    private static bool IsTalismanKeyword(string line)
        => TalismanKeywords.Contains(line) || CharmPattern.IsMatch(line);

    /// <summary>Strips x prefix (multiplicative), a leading rolled value (e.g. "24% " in
    /// "x24% Physical Damage Multiplier"), and ↑ suffix (Greater Affix marker), leaving the affix name.</summary>
    private static (string Name, bool IsGa) StripAffixModifiers(string raw)
    {
        var name = raw.TrimStart();
        if (name.StartsWith('x') || name.StartsWith('X'))
            name = name[1..].TrimStart();

        // Maxroll prefixes multiplicative affixes with their rolled value ("24% Physical Damage
        // Multiplier"); strip that leading number/percent so the affix NAME remains for resolution.
        name = LeadingValueRegex().Replace(name, string.Empty);

        var isGa = name.EndsWith('↑');
        if (isGa)
            name = name[..^1].TrimEnd();
        return (name, isGa);
    }

    [GeneratedRegex(@"^Charm\s*\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex CharmRegex();

    [GeneratedRegex(@"^\d+(?:\.\d+)?%?\s*")]
    private static partial Regex LeadingValueRegex();
}
