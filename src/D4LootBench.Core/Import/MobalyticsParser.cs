using System.Text.RegularExpressions;

namespace D4LootBench.Core.Import;

/// <summary>
/// Parses the gear section copied from a Mobalytics build guide page.
/// Format: slot-index integer → slot name → item name → "toggle modifiers" → priority/affix pairs →
///         blank → temper line → socket lines (discarded).
/// </summary>
public sealed partial class MobalyticsParser : IBuildGuideParser
{
    private enum State { Idle, SlotName, ItemName, WaitToggle, AffixList, PreTemper, Sockets }

    public ParsedBuildGuide Parse(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var slots = new List<ParsedSlot>();

        var state = State.Idle;
        string? slotLabel = null;
        string? itemName = null;
        var currentPriority = 0;
        var affixes = new List<ParsedAffix>();
        var prevWasBlank = true;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            var isBlank = string.IsNullOrEmpty(line);

            switch (state)
            {
                case State.Idle:
                    if (!isBlank && int.TryParse(line, out _))
                        state = State.SlotName;
                    break;

                case State.SlotName:
                    if (isBlank) break;
                    slotLabel = line;
                    state = State.ItemName;
                    break;

                case State.ItemName:
                    if (isBlank) break;
                    if (line == "toggle modifiers")
                        state = State.AffixList;
                    else
                    {
                        itemName = line;
                        state = State.WaitToggle;
                    }
                    break;

                case State.WaitToggle:
                    if (line == "toggle modifiers")
                        state = State.AffixList;
                    break;

                case State.AffixList:
                    ProcessAffixLine(line, isBlank, ref state, ref currentPriority, affixes);
                    break;

                case State.PreTemper:
                    if (isBlank) break;
                    if (TemperPattern().IsMatch(line))
                    {
                        EmitSlot();
                        state = State.Sockets;
                    }
                    else if (int.TryParse(line, out _))
                    {
                        // Slot separator appeared without a temper line
                        EmitSlot();
                        state = State.SlotName;
                    }
                    else
                    {
                        // Some guides omit the blank line before the temper — treat as still in affixes
                        state = State.AffixList;
                        ProcessAffixLine(line, isBlank, ref state, ref currentPriority, affixes);
                    }
                    break;

                case State.Sockets:
                    // Blank line followed by an integer signals the next slot separator.
                    // Socket-section indices (6, 7, …) appear without a preceding blank.
                    if (prevWasBlank && !isBlank && int.TryParse(line, out _))
                        state = State.SlotName;
                    break;
            }

            prevWasBlank = isBlank;
        }

        EmitSlot();

        return new ParsedBuildGuide { DetectedFormat = BuildGuideFormat.Mobalytics, Slots = slots };

        void EmitSlot()
        {
            if (slotLabel is null) return;
            slots.Add(new ParsedSlot
            {
                SlotLabel = slotLabel,
                ItemName = itemName,
                Affixes = [.. affixes]
            });
            slotLabel = null;
            itemName = null;
            currentPriority = 0;
            affixes.Clear();
        }
    }

    private static void ProcessAffixLine(
        string line, bool isBlank,
        ref State state, ref int currentPriority,
        List<ParsedAffix> affixes)
    {
        if (isBlank)
        {
            state = State.PreTemper;
            return;
        }
        if (int.TryParse(line, out var priority))
        {
            currentPriority = priority;
            return;
        }
        // Priority 5 = aspect/unique imprint slot — no searchable affix follows
        if (currentPriority is >= 1 and <= 4)
            affixes.Add(new ParsedAffix { RawName = line, Priority = currentPriority });
    }

    [GeneratedRegex(@"^\w[\w\s]+:\s.+\(.+\)$")]
    private static partial Regex TemperPattern();
}
