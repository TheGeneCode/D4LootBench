using D4Loot.Core.Models;

namespace D4Loot.Core.Codec;

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
        var bytes = Convert.FromBase64String(shareCode.Trim());
        var reader = new ProtoReader(bytes);

        var rules = new List<FilterRule>();
        var name = "Unnamed Filter";

        while (reader.HasData)
        {
            var (field, wire) = reader.ReadTag();
            switch (field)
            {
                case 1: rules.Add(DecodeRule(reader.ReadLenBytes())); break;
                case 2: name = reader.ReadString(); break;
                case 3:
                case 4: reader.ReadVarint(); break;
                default: reader.Skip(wire); break;
            }
        }

        return new FilterRuleset(name, rules);
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
                case 1: name = reader.ReadString(); break;
                case 2: visibility = (Visibility)(int)reader.ReadVarint(); break;
                case 3: color = reader.ReadFixed32(); break;
                case 4: conditions.Add(DecodeCondition(reader.ReadLenBytes())); break;
                case 5: isEnabled = reader.ReadVarint() != 0; break;
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
            case GreaterAffixCondition ga:
                buf.AddRange(ProtoWriter.VarintField(1, 3));
                buf.AddRange(ProtoWriter.VarintField(6, (ulong)ga.MinimumCount));
                break;
            case CodexCondition:
                buf.AddRange(ProtoWriter.VarintField(1, 4));
                buf.AddRange(ProtoWriter.VarintField(4, 1));
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
                buf.AddRange(ProtoWriter.VarintField(4, (ulong)a.MinimumCount));
                break;
            case OptionalAffixCondition oa:
                buf.AddRange(ProtoWriter.VarintField(1, 7));
                foreach (var id in oa.AffixIds)
                    buf.AddRange(ProtoWriter.Fixed32Field(2, id));
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

        var condType = 0;
        var field4 = 0UL;
        var field5 = 0UL;
        var field6 = 0UL;
        var ids = new List<uint>();

        while (reader.HasData)
        {
            var (field, wire) = reader.ReadTag();
            switch (field)
            {
                case 1: condType = (int)reader.ReadVarint(); break;
                case 2: ids.Add(reader.ReadFixed32()); break;
                case 4: field4 = reader.ReadVarint(); break;
                case 5: field5 = reader.ReadVarint(); break;
                case 6: field6 = reader.ReadVarint(); break;
                default: reader.Skip(wire); break;
            }
        }

        return condType switch
        {
            0 => new ItemPowerCondition((int)field4, (int)field5),
            1 => new RarityCondition((RarityFlags)field4),
            2 => new ItemPropertiesCondition((int)field4),
            3 => new GreaterAffixCondition((int)field6),
            4 => new CodexCondition(),
            5 => new ItemTypeCondition(ids),
            6 => new AffixCondition(ids, (int)field4),
            7 => new OptionalAffixCondition(ids),
            _ => new UnknownCondition(condType, condBytes)
        };
    }
}
