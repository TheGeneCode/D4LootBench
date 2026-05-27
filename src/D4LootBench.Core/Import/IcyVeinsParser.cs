namespace D4LootBench.Core.Import;

/// <summary>
/// Parses the gear affixes table copied from an Icy Veins build guide page.
/// Format: tab-delimited rows. Header row containing "Gear Affixes" + tab is the format fingerprint.
/// Each data row: slot-name TAB "N. AffixName" [TAB temper-column (discarded)].
/// Continuation rows: "N. AffixName" with no leading tab.
/// </summary>
public sealed class IcyVeinsParser : IBuildGuideParser
{
    private enum State { Idle, Rows, AffixContinuation }

    public ParsedBuildGuide Parse(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var slots = new List<ParsedSlot>();

        var state = State.Idle;
        string? slotLabel = null;
        var affixes = new List<ParsedAffix>();
        var priorityCounter = 0;

        foreach (var rawLine in lines)
        {
            var isBlank = string.IsNullOrWhiteSpace(rawLine);

            switch (state)
            {
                case State.Idle:
                    // Header row: any line containing "Gear Affixes" (with or without tabs)
                    if (!isBlank && rawLine.Contains("Gear Affixes", StringComparison.OrdinalIgnoreCase))
                        state = State.Rows;
                    break;

                case State.Rows:
                    if (isBlank) break;
                    if (rawLine.Contains('\t'))
                    {
                        // New slot row: "SlotName\t1. AffixName[...temper]"
                        EmitSlot();
                        var tabIdx = rawLine.IndexOf('\t');
                        slotLabel = rawLine[..tabIdx].Trim();
                        priorityCounter = 0;
                        var affixPart = StripTemperColumn(rawLine[(tabIdx + 1)..]);
                        TryAddNumberedAffix(affixPart, affixes, ref priorityCounter);
                        state = State.AffixContinuation;
                    }
                    else
                    {
                        // Slot name on its own line (no tab to first affix)
                        var trimmed = rawLine.Trim();
                        if (!IsNumberedAffix(trimmed) && !trimmed.StartsWith('+'))
                        {
                            EmitSlot();
                            slotLabel = trimmed;
                            priorityCounter = 0;
                            state = State.AffixContinuation;
                        }
                    }
                    break;

                case State.AffixContinuation:
                    if (isBlank)
                    {
                        state = State.Rows;
                        break;
                    }
                    if (rawLine.Contains('\t'))
                    {
                        var tabIdx = rawLine.IndexOf('\t');
                        var left = rawLine[..tabIdx].Trim();
                        if (IsNumberedAffix(left))
                        {
                            // Last affix with inline temper column — add affix, temper discarded
                            TryAddNumberedAffix(left, affixes, ref priorityCounter);
                        }
                        else if (string.IsNullOrEmpty(left))
                        {
                            // Browser multi-line cell paste: empty first column is a continuation row.
                            // Strip the second tab (temper column) before parsing — TryAddNumberedAffix
                            // ignores lines that don't start with "N." so temper/+ lines are silently dropped.
                            var affixPart = StripTemperColumn(rawLine[(tabIdx + 1)..]);
                            TryAddNumberedAffix(affixPart, affixes, ref priorityCounter);
                        }
                        else
                        {
                            // New slot row
                            EmitSlot();
                            slotLabel = left;
                            priorityCounter = 0;
                            var affixPart = StripTemperColumn(rawLine[(tabIdx + 1)..]);
                            TryAddNumberedAffix(affixPart, affixes, ref priorityCounter);
                        }
                    }
                    else
                    {
                        var trimmed = rawLine.Trim();
                        if (IsNumberedAffix(trimmed))
                        {
                            TryAddNumberedAffix(trimmed, affixes, ref priorityCounter);
                        }
                        else if (!trimmed.StartsWith('+'))
                        {
                            // New slot name on its own line; starts-with-'+' lines are tempering affixes
                            EmitSlot();
                            slotLabel = trimmed;
                            priorityCounter = 0;
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        EmitSlot();

        return new ParsedBuildGuide { DetectedFormat = BuildGuideFormat.IcyVeins, Slots = slots };

        void EmitSlot()
        {
            if (slotLabel is null) return;
            slots.Add(new ParsedSlot { SlotLabel = slotLabel, Affixes = [.. affixes] });
            slotLabel = null;
            affixes.Clear();
            priorityCounter = 0;
        }
    }

    /// <summary>Strips everything from the first tab onward (temper column).</summary>
    private static string StripTemperColumn(string s)
    {
        var idx = s.IndexOf('\t');
        return idx >= 0 ? s[..idx].Trim() : s.Trim();
    }

    /// <summary>Returns true if the line starts with "N. " (Icy Veins numbered affix format).</summary>
    private static bool IsNumberedAffix(string s)
        => s.Length > 2 && char.IsDigit(s[0]) && s[1] == '.';

    private static void TryAddNumberedAffix(string line, List<ParsedAffix> affixes, ref int counter)
    {
        if (!IsNumberedAffix(line)) return;
        // Strip "N. " prefix
        var dotIdx = line.IndexOf('.');
        var name = line[(dotIdx + 1)..].Trim();
        if (string.IsNullOrEmpty(name)) return;
        counter++;
        affixes.Add(new ParsedAffix { RawName = name, Priority = counter });
    }
}
