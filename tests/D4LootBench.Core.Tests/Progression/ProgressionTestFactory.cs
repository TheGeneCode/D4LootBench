namespace D4LootBench.Core.Tests.Progression;

using D4LootBench.Core.Gear;
using D4LootBench.Core.Progression;

// Tiny local factory helpers for building GearItem/GearAffix in progression tests over raw uint hashes
// (no FilterDataContext needed).
internal static class ProgressionTestFactory
{
    public static GearAffix Affix(uint hash, bool greater = false) => new()
    {
        RawText = $"affix#{hash}",
        AffixHash = hash,
        IsGreaterAffix = greater,
    };

    public static GearAffix UnresolvedAffix() => new() { RawText = "unresolved", AffixHash = null };

    public static GearItem Item(GearSlot slot, IEnumerable<GearAffix> affixes, uint? uniqueHash = null) => new()
    {
        Slot = slot,
        UniqueHash = uniqueHash,
        Affixes = affixes.ToList(),
    };

    // Directly-constructed NeedsRule diff so budget-overflow tests aren't limited to the ~9 real slots.
    public static SlotDiff NeedsRule(GearSlot slot, int ordinal, params uint[] targets) => new()
    {
        Slot = new SlotKey(slot, ordinal),
        Status = SlotDiffStatus.NeedsRule,
        TargetAffixIds = targets,
    };
}
