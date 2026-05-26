namespace D4LootBench.Core.Serialization;

public sealed class AnnotatedAffixListConverter : AnnotatedHashListConverter
{
    protected override string GetName(uint id) => FilterDataContext.Current.Affixes.GetDisplayName(id);

    protected override bool TryLookupByName(string name, out uint id)
    {
        if (FilterDataContext.Current.Affixes.TryGetByName(name, out var entry))
        {
            id = entry.Hash;
            return true;
        }
        id = 0;
        return false;
    }
}

public sealed class AnnotatedItemTypeListConverter : AnnotatedHashListConverter
{
    protected override string GetName(uint id) => FilterDataContext.Current.ItemTypes.GetDisplayName(id);

    protected override bool TryLookupByName(string name, out uint id)
    {
        if (FilterDataContext.Current.ItemTypes.TryGetByName(name, out var entry))
        {
            id = entry.Hash;
            return true;
        }
        id = 0;
        return false;
    }
}

public sealed class AnnotatedUniqueListConverter : AnnotatedHashListConverter
{
    protected override string GetName(uint id) => FilterDataContext.Current.Uniques.GetDisplayName(id);

    protected override bool TryLookupByName(string name, out uint id)
    {
        if (FilterDataContext.Current.Uniques.TryGetByName(name, out var entry))
        {
            id = entry.SnoId;
            return true;
        }
        id = 0;
        return false;
    }
}

public sealed class AnnotatedTalismanSetListConverter : AnnotatedHashListConverter
{
    protected override string GetName(uint id) => FilterDataContext.Current.TalismanSets.GetSetName(id);

    protected override bool TryLookupByName(string name, out uint id)
    {
        var match = FilterDataContext.Current.TalismanSets.All
            .FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));
        if (match is not null) { id = match.Hash; return true; }
        id = 0;
        return false;
    }
}
