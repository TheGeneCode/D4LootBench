namespace D4LootBench.Core.Data;

/// <summary>
/// Default <see cref="IFilterDataService"/> implementation. Each catalog delegates to the
/// existing static <c>*Database</c> singletons, so behavior is identical to direct access —
/// the indirection exists only to give consumers an injectable seam.
/// </summary>
public sealed class FilterDataService : IFilterDataService
{
    public IAffixCatalog Affixes { get; } = new AffixCatalog();
    public ISkillCatalog Skills { get; } = new SkillCatalog();
    public IItemTypeCatalog ItemTypes { get; } = new ItemTypeCatalog();
    public IUniqueItemCatalog Uniques { get; } = new UniqueItemCatalog();
    public ITalismanSetCatalog TalismanSets { get; } = new TalismanSetCatalog();

    private sealed class AffixCatalog : IAffixCatalog
    {
        public IReadOnlyList<AffixEntry> All => AffixDatabase.All;
        public IReadOnlyDictionary<uint, AffixEntry> ByHash => AffixDatabase.ByHash;
        public IReadOnlyList<AffixEntry> ForClass(string className) => AffixDatabase.ForClass(className);
        public string GetDisplayName(uint hash) => AffixDatabase.GetDisplayName(hash);
        public bool TryGetByName(string name, out AffixEntry entry) =>
            AffixDatabase.ByName.TryGetValue(name, out entry!);
    }

    private sealed class SkillCatalog : ISkillCatalog
    {
        public IReadOnlyList<SkillEntry> All => SkillDatabase.All;
        public IReadOnlyDictionary<uint, SkillEntry> ByHash => SkillDatabase.ByHash;
        public IReadOnlyList<SkillEntry> ForClass(string className) => SkillDatabase.ForClass(className);
        public string GetDisplayName(uint hash) => SkillDatabase.GetDisplayName(hash);
    }

    private sealed class ItemTypeCatalog : IItemTypeCatalog
    {
        public IReadOnlyList<ItemTypeEntry> All => ItemTypeDatabase.All;
        public IReadOnlyDictionary<uint, ItemTypeEntry> ByHash => ItemTypeDatabase.ByHash;
        public IReadOnlyList<ItemTypeEntry> ForClass(string className) => ItemTypeDatabase.ForClass(className);
        public string GetDisplayName(uint hash) => ItemTypeDatabase.GetDisplayName(hash);
        public bool TryGetByName(string name, out ItemTypeEntry entry)
        {
            entry = ItemTypeDatabase.All.FirstOrDefault(e =>
                string.Equals(e.Name, name, StringComparison.Ordinal))!;
            return entry is not null;
        }
    }

    private sealed class UniqueItemCatalog : IUniqueItemCatalog
    {
        public IReadOnlyList<UniqueItemEntry> All => UniqueItemDatabase.All;
        public IReadOnlyList<UniqueItemEntry> Released => UniqueItemDatabase.Released;
        public IReadOnlyDictionary<uint, UniqueItemEntry> ByHash => UniqueItemDatabase.ByHash;
        public IReadOnlyList<UniqueItemEntry> ForClass(string className) => UniqueItemDatabase.ForClass(className);
        public string GetDisplayName(uint hash) => UniqueItemDatabase.GetDisplayName(hash);
        public bool TryGetByName(string name, out UniqueItemEntry entry)
        {
            entry = UniqueItemDatabase.Released.FirstOrDefault(e =>
                string.Equals(e.Name, name, StringComparison.Ordinal))!;
            return entry is not null;
        }
    }

    private sealed class TalismanSetCatalog : ITalismanSetCatalog
    {
        public IReadOnlyList<TalismanSetInfo> All => TalismanSetDatabase.All;
        public IReadOnlyDictionary<uint, TalismanSetInfo> ByHash => TalismanSetDatabase.ByHash;
        public IReadOnlyDictionary<uint, TalismanSetItemEntry> ItemsByHash => TalismanSetDatabase.ItemsByHash;
        public IReadOnlyDictionary<uint, uint> ItemToSetHash => TalismanSetDatabase.ItemToSetHash;
        public IReadOnlyList<TalismanSetInfo> ForClass(string className) => TalismanSetDatabase.ForClass(className);
        public string GetSetName(uint hash) => TalismanSetDatabase.GetSetName(hash);
        public string GetItemName(uint hash) => TalismanSetDatabase.GetItemName(hash);
        public uint GetSetHashForItem(uint itemHash) => TalismanSetDatabase.GetSetHashForItem(itemHash);
    }
}
