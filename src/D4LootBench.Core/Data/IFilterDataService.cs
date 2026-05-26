namespace D4LootBench.Core.Data;

/// <summary>
/// Aggregates the per-domain catalogs (affixes, skills, item types, uniques, talisman sets)
/// behind a single injectable service so consumers (ViewModels, AI assistant, validators)
/// can be constructed without a hard dependency on the static <c>*Database</c> singletons.
/// </summary>
public interface IFilterDataService
{
    IAffixCatalog Affixes { get; }
    ISkillCatalog Skills { get; }
    IItemTypeCatalog ItemTypes { get; }
    IUniqueItemCatalog Uniques { get; }
    ITalismanSetCatalog TalismanSets { get; }
}

public interface IAffixCatalog
{
    IReadOnlyList<AffixEntry> All { get; }
    IReadOnlyDictionary<uint, AffixEntry> ByHash { get; }
    IReadOnlyList<AffixEntry> ForClass(string className);
    string GetDisplayName(uint hash);
    bool TryGetByName(string name, out AffixEntry entry);
}

public interface ISkillCatalog
{
    IReadOnlyList<SkillEntry> All { get; }
    IReadOnlyDictionary<uint, SkillEntry> ByHash { get; }
    IReadOnlyList<SkillEntry> ForClass(string className);
    string GetDisplayName(uint hash);
}

public interface IItemTypeCatalog
{
    IReadOnlyList<ItemTypeEntry> All { get; }
    IReadOnlyDictionary<uint, ItemTypeEntry> ByHash { get; }
    IReadOnlyList<ItemTypeEntry> ForClass(string className);
    string GetDisplayName(uint hash);
    bool TryGetByName(string name, out ItemTypeEntry entry);
}

public interface IUniqueItemCatalog
{
    IReadOnlyList<UniqueItemEntry> All { get; }
    IReadOnlyList<UniqueItemEntry> Released { get; }
    IReadOnlyDictionary<uint, UniqueItemEntry> ByHash { get; }
    IReadOnlyList<UniqueItemEntry> ForClass(string className);
    string GetDisplayName(uint hash);
    bool TryGetByName(string name, out UniqueItemEntry entry);
}

public interface ITalismanSetCatalog
{
    IReadOnlyList<TalismanSetInfo> All { get; }
    IReadOnlyDictionary<uint, TalismanSetInfo> ByHash { get; }
    IReadOnlyDictionary<uint, TalismanSetItemEntry> ItemsByHash { get; }
    IReadOnlyDictionary<uint, uint> ItemToSetHash { get; }
    IReadOnlyList<TalismanSetInfo> ForClass(string className);
    string GetSetName(uint hash);
    string GetItemName(uint hash);
    uint GetSetHashForItem(uint itemHash);
}
