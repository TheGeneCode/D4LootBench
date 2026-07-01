namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Gear;

/// <summary>The player's current gear, keyed by physical <see cref="SlotKey"/> (one item per key).</summary>
public sealed class EquippedLoadout
{
    private readonly IReadOnlyDictionary<SlotKey, GearItem> _items;

    /// <summary>Initializes a new instance of the <see cref="EquippedLoadout"/> class from an explicit slot–item map.</summary>
    /// <param name="items">The slot-to-item map.</param>
    public EquippedLoadout(IReadOnlyDictionary<SlotKey, GearItem> items) => _items = items;

    /// <summary>Gets the equipped items keyed by slot.</summary>
    public IReadOnlyDictionary<SlotKey, GearItem> Items => _items;

    /// <summary>Gets every occupied slot key.</summary>
    public IReadOnlyCollection<SlotKey> Slots => (IReadOnlyCollection<SlotKey>)_items.Keys;

    /// <summary>Gets the item in <paramref name="key"/>, or <c>null</c> when empty.</summary>
    /// <param name="key">The slot key to look up.</param>
    /// <returns>The equipped item, or <c>null</c> when the slot is empty.</returns>
    public GearItem? this[SlotKey key] => _items.TryGetValue(key, out var v) ? v : null;

    /// <summary>Builds a loadout from a flat item list, assigning ordinals per <see cref="GearSlot"/> in
    /// enumeration order. Items with <see cref="GearSlot.Unknown"/> are skipped (undiffable).</summary>
    /// <param name="items">The flat item list.</param>
    /// <returns>A loadout keyed by <see cref="SlotKey"/>.</returns>
    public static EquippedLoadout FromItems(IEnumerable<GearItem> items)
    {
        var map = new Dictionary<SlotKey, GearItem>();
        var next = new Dictionary<GearSlot, int>();
        foreach (var item in items)
        {
            if (item.Slot == GearSlot.Unknown)
            {
                continue;
            }

            var ordinal = next.TryGetValue(item.Slot, out var n) ? n : 0;
            next[item.Slot] = ordinal + 1;
            map[new SlotKey(item.Slot, ordinal)] = item;
        }

        return new EquippedLoadout(map);
    }
}
