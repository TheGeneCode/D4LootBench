using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.Core.Codec;

public static class FilterCodec
{
    public static string Encode(FilterRuleset ruleset)
    {
        var buffer = new List<byte>();

        foreach (var rule in ruleset.Rules)
            buffer.AddRange(ProtoWriter.LenField(1, EncodeRule(rule)));

        buffer.AddRange(ProtoWriter.StringField(2, ruleset.Name));
        buffer.AddRange(ProtoWriter.VarintField(3, (ulong)ruleset.Rules.Count));
        buffer.AddRange(ProtoWriter.VarintField(4, 1));

        return Convert.ToBase64String([.. buffer]);
    }

    public static FilterRuleset Decode(string shareCode)
    {
        var trimmed = shareCode.Trim();
        var bytes = Convert.FromBase64String(trimmed);
        var reader = new ProtoReader(bytes);

        var rules = new List<FilterRule>();
        var name = "Unnamed Filter";

        while (reader.HasData)
        {
            var (field, wire) = reader.ReadTag();
            switch (field)
            {
                case 1:
                    if (wire == 2)
                    {
                        var ruleBytes = reader.ReadLenBytes();
                        var rule = DecodeRule(ruleBytes);

                        var hasOverflow = rule.Conditions.Any(c => c is AffixCondition { Field5: not 0 });
                        if (hasOverflow)
                        {
                            var fixedConds = rule.Conditions
                                .Select(c => c is AffixCondition ac ? ac with { Field5 = 0 } : c).ToList();
                            rule = new FilterRule(rule.Name, rule.Visibility, rule.Color, fixedConds, rule.IsEnabled);
                            var fixedBytes = EncodeRule(rule);
                            reader.Seek(reader.Position - (ruleBytes.Length - fixedBytes.Length));
                        }

                        rules.Add(rule);
                        break;
                    }
                    goto default;
                case 2:
                    if (wire == 2) { name = reader.ReadString(); break; }
                    goto default;
                case 3:
                    if (wire == 0) { reader.ReadVarint(); break; }
                    goto default;
                case 4:
                    if (wire == 0) { reader.ReadVarint(); break; }
                    goto default;
                default: reader.Skip(wire); break;
            }
        }

        return new FilterRuleset(name, rules) { OriginalCode = trimmed };
    }

    // ── Rule ─────────────────────────────────────────────────────────

    private static byte[] EncodeRule(FilterRule rule)
    {
        var buf = new List<byte>();
        buf.AddRange(ProtoWriter.StringField(1, rule.Name));
        buf.AddRange(ProtoWriter.VarintField(2, (ulong)rule.Visibility));
        if (rule.Color != 0)
            buf.AddRange(ProtoWriter.Fixed32Field(3, rule.Color));
        foreach (var cond in rule.Conditions)
            buf.AddRange(ProtoWriter.LenField(4, EncodeCondition(cond)));
        buf.AddRange(ProtoWriter.VarintField(5, rule.IsEnabled ? 1UL : 0UL));
        return [.. buf];
    }

    private static FilterRule DecodeRule(byte[] ruleBytes)
    {
        var reader = new ProtoReader(ruleBytes);

        var name = "";
        var visibility = Visibility.Show;
        var color = 0u;
        var conditions = new List<Condition>();
        var isEnabled = true;

        while (reader.HasData)
        {
            var (field, wire) = reader.ReadTag();
            switch (field)
            {
                case 1:
                    if (wire == 2) name = reader.ReadString(); else reader.Skip(wire);
                    break;
                case 2:
                    if (wire == 0) visibility = (Visibility)(int)reader.ReadVarint(); else reader.Skip(wire);
                    break;
                case 3:
                    if (wire == 5) color = reader.ReadFixed32(); else reader.Skip(wire);
                    break;
                case 4:
                    if (wire == 2) conditions.Add(DecodeCondition(reader.ReadLenBytes())); else reader.Skip(wire);
                    break;
                case 5:
                    if (wire == 0) isEnabled = reader.ReadVarint() != 0; else reader.Skip(wire);
                    break;
                default: reader.Skip(wire); break;
            }
        }

        return new FilterRule(name, visibility, color, conditions, isEnabled);
    }

    // ── Condition ────────────────────────────────────────────────────

    private static byte[] EncodeCondition(Condition condition)
    {
        var buf = new List<byte>();
        switch (condition)
        {
            case ItemPowerCondition ip:
                buf.AddRange(ProtoWriter.VarintField(1, 0));
                buf.AddRange(ProtoWriter.VarintField(4, (ulong)ip.Minimum));
                if (ip.Maximum != 0)
                    buf.AddRange(ProtoWriter.VarintField(5, (ulong)ip.Maximum));
                break;
            case RarityCondition r:
                buf.AddRange(ProtoWriter.VarintField(1, 1));
                buf.AddRange(ProtoWriter.VarintField(4, (ulong)r.Mask));
                break;
            case ItemPropertiesCondition ip2:
                buf.AddRange(ProtoWriter.VarintField(1, 2));
                buf.AddRange(ProtoWriter.VarintField(4, (ulong)ip2.PropertyMask));
                break;
            case CodexCondition:
                buf.AddRange(ProtoWriter.VarintField(1, 3));
                buf.AddRange(ProtoWriter.VarintField(6, 1));
                break;
            case GreaterAffixCondition ga:
                buf.AddRange(ProtoWriter.VarintField(1, 4));
                buf.AddRange(ProtoWriter.VarintField(4, (ulong)ga.MinimumCount));
                buf.AddRange(ProtoWriter.VarintField(6, 1));
                break;
            case ItemTypeCondition it:
                buf.AddRange(ProtoWriter.VarintField(1, 5));
                foreach (var id in it.TypeIds)
                    buf.AddRange(ProtoWriter.Fixed32Field(2, id));
                break;
            case AffixCondition a:
                buf.AddRange(ProtoWriter.VarintField(1, 6));
                foreach (var id in a.AffixIds)
                    buf.AddRange(ProtoWriter.Fixed32Field(2, id));
                foreach (var ge in a.GreaterEntries)
                {
                    var inner = new List<byte>();
                    inner.AddRange(ProtoWriter.Fixed32Field(1, ge.AffixId));
                    inner.AddRange(ProtoWriter.Fixed32Field(2, ge.AffixIdEcho));
                    buf.AddRange(ProtoWriter.LenField(3, [.. inner]));
                }
                buf.AddRange(ProtoWriter.VarintField(4, (ulong)a.MinimumCount));
                if (a.Field5 != 0)
                    buf.AddRange(ProtoWriter.VarintField(5, (ulong)a.Field5));
                break;
            case OptionalAffixCondition oa:
                buf.AddRange(ProtoWriter.VarintField(1, 7));
                foreach (var id in oa.AffixIds)
                    buf.AddRange(ProtoWriter.Fixed32Field(2, id));
                foreach (var ge in oa.GreaterEntries)
                {
                    var inner = new List<byte>();
                    inner.AddRange(ProtoWriter.Fixed32Field(1, ge.AffixId));
                    inner.AddRange(ProtoWriter.Fixed32Field(2, ge.AffixIdEcho));
                    buf.AddRange(ProtoWriter.LenField(3, [.. inner]));
                }
                if (oa.MinimumCount != 0)
                    buf.AddRange(ProtoWriter.VarintField(4, (ulong)oa.MinimumCount));
                if (oa.Field5 != 0)
                    buf.AddRange(ProtoWriter.VarintField(5, (ulong)oa.Field5));
                break;
            case SpecificUniqueCondition su:
                buf.AddRange(ProtoWriter.VarintField(1, 8));
                foreach (var id in su.UniqueIds)
                    buf.AddRange(ProtoWriter.Fixed32Field(2, id));
                break;
            case TalismanSetCondition ts:
                buf.AddRange(ProtoWriter.VarintField(1, 9));
                foreach (var id in ts.SetIds)
                    buf.AddRange(ProtoWriter.Fixed32Field(2, id));
                foreach (var group in ts.SetEntries.GroupBy(e => e.SetId))
                {
                    var inner = new List<byte>();
                    inner.AddRange(ProtoWriter.Fixed32Field(1, group.Key));
                    foreach (var se in group)
                        inner.AddRange(ProtoWriter.Fixed32Field(2, se.ItemId));
                    buf.AddRange(ProtoWriter.LenField(3, [.. inner]));
                }
                break;
            case UnknownCondition u:
                buf.AddRange(u.RawBytes);
                break;
        }
        return [.. buf];
    }

    private static Condition DecodeCondition(byte[] condBytes)
    {
        var reader = new ProtoReader(condBytes);

        var condType = -1;
        var field4 = 0UL;
        var field5 = 0UL;
        var field6 = 0UL;
        var ids = new List<uint>();
        var greaterEntries = new List<GreaterAffixEntry>();

        while (reader.HasData)
        {
            var (field, wire) = reader.ReadTag();
            switch (field)
            {
                case 1:
                    if (condType == -1)
                        condType = (int)reader.ReadVarint();
                    else
                        return BuildCondition(condType, field4, field5, field6, ids, greaterEntries, condBytes);
                    break;
                case 2: ids.Add(reader.ReadFixed32()); break;
                case 3:
                {
                    var entryBytes = reader.ReadLenBytes();
                    if (entryBytes.Length >= 10)
                    {
                        var er = new ProtoReader(entryBytes);
                        uint? setField = null;
                        var itemFields = new List<uint>();
                        while (er.HasData)
                        {
                            var (ef, ew) = er.ReadTag();
                            if (ef == 1 && ew == 5) setField = er.ReadFixed32();
                            else if (ef == 2 && ew == 5) itemFields.Add(er.ReadFixed32());
                            else er.Skip(ew);
                        }
                        if (setField.HasValue)
                        {
                            foreach (var item in itemFields)
                                greaterEntries.Add(new GreaterAffixEntry(setField.Value, item));
                        }
                    }
                    break;
                }
                case 4: field4 = reader.ReadVarint(); break;
                case 5: field5 = reader.ReadVarint(); break;
                case 6: field6 = reader.ReadVarint(); break;
                default: reader.Skip(wire); break;
            }
        }

        return BuildCondition(condType, field4, field5, field6, ids, greaterEntries, condBytes);
    }

    private static Condition BuildCondition(int condType, ulong field4, ulong field5, ulong field6,
        List<uint> ids, List<GreaterAffixEntry> greaterEntries, byte[] condBytes)
    {
        return condType switch
        {
            0 => new ItemPowerCondition((int)field4, (int)field5),
            1 => new RarityCondition((RarityFlags)field4),
            2 => new ItemPropertiesCondition((int)field4),
            3 => new CodexCondition(),
            4 => new GreaterAffixCondition((int)field4),
            5 => new ItemTypeCondition(ids),
            6 => new AffixCondition(ids, (int)field4) { GreaterEntries = greaterEntries, Field5 = (int)field5 },
            7 => new OptionalAffixCondition(ids, (int)field4) { GreaterEntries = greaterEntries, Field5 = (int)field5 },
            8 => new SpecificUniqueCondition(ids),
            9 => new TalismanSetCondition
            {
                SetIds = ids,
                SetEntries = greaterEntries.Select(e => new TalismanSetEntry(e.AffixId, e.AffixIdEcho)).ToList()
            },
            _ => new UnknownCondition(condType, condBytes)
        };
    }
}
