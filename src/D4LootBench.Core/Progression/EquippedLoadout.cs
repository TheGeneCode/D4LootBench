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

    /// <summary>Builds a loadout from a flat item list. Weapon/Offhand items with a known
    /// <see cref="GearItem.ItemTypeName"/> are keyed by the class-aware <see cref="WeaponSlotRole"/> they
    /// play (via <paramref name="roleMap"/>) — a role admits a whole set of item types, so a Barbarian's
    /// Slicing weapons all share one slot; a second one-handed weapon is promoted to the Offhand role for
    /// dual-wield. When <paramref name="roleMap"/> is <c>null</c> (legacy callers) weapon/offhand items key
    /// to a single roleless slot per <see cref="GearSlot"/> (last wins). Items whose type does not resolve
    /// to a weapon role fall through to ordinal keying. All non-weapon items are keyed by an ordinal
    /// assigned per <see cref="GearSlot"/> in enumeration order. Items with <see cref="GearSlot.Unknown"/>
    /// are skipped (undiffable).</summary>
    /// <param name="items">The flat item list.</param>
    /// <param name="cls">The player class (drives weapon-role classification).</param>
    /// <param name="roleMap">The weapon role map; <c>null</c> keeps legacy roleless weapon keying.</param>
    /// <returns>A loadout keyed by <see cref="SlotKey"/>.</returns>
    public static EquippedLoadout FromItems(
        IEnumerable<GearItem> items,
        PlayerClass cls = PlayerClass.All,
        WeaponRoleMap? roleMap = null)
        => FromItems(items, out _, cls, roleMap);

    /// <summary>Builds a loadout from a flat item list, surfacing a warning for every slot collision.
    /// Keying is identical to <see cref="FromItems(IEnumerable{GearItem}, PlayerClass, WeaponRoleMap?)"/>;
    /// this overload additionally reports (via <paramref name="warnings"/>) each time two items resolve to
    /// the same <see cref="SlotKey"/> — e.g. the same physical slot snipped twice, or two weapons the class
    /// plays in the same role — where last-wins keying silently drops the earlier item.</summary>
    /// <param name="items">The flat item list.</param>
    /// <param name="warnings">Receives one message per dropped item; empty when no slot collided.</param>
    /// <param name="cls">The player class (drives weapon-role classification).</param>
    /// <param name="roleMap">The weapon role map; <c>null</c> keeps legacy roleless weapon keying.</param>
    /// <returns>A loadout keyed by <see cref="SlotKey"/>.</returns>
    public static EquippedLoadout FromItems(
        IEnumerable<GearItem> items,
        out IReadOnlyList<string> warnings,
        PlayerClass cls = PlayerClass.All,
        WeaponRoleMap? roleMap = null)
    {
        var map = new Dictionary<SlotKey, GearItem>();
        var next = new Dictionary<GearSlot, int>();
        var usedRoles = new HashSet<WeaponSlotRole>();
        var warns = new List<string>();

        void Put(SlotKey key, GearItem item)
        {
            if (map.TryGetValue(key, out var existing))
            {
                warns.Add(
                    $"Two items resolve to the same {key} slot; keeping {Describe(item)} and dropping {Describe(existing)}.");
            }

            map[key] = item;
        }

        foreach (var item in items)
        {
            if (item.Slot == GearSlot.Unknown)
            {
                continue;
            }

            if (item.Slot is GearSlot.Weapon or GearSlot.Offhand && item.ItemTypeName is not null)
            {
                if (roleMap is null)
                {
                    Put(new SlotKey(item.Slot, 0, WeaponSlotRole.None), item);
                    continue;
                }

                var role = roleMap.RoleForItemType(item.ItemTypeName, cls);
                if (role != WeaponSlotRole.None)
                {
                    var slot = item.Slot;

                    // Dual-wield: a second one-handed Mainhand becomes the Offhand.
                    if (role == WeaponSlotRole.Mainhand && !usedRoles.Add(WeaponSlotRole.Mainhand))
                    {
                        role = WeaponSlotRole.Offhand;
                        slot = GearSlot.Offhand;
                    }

                    Put(new SlotKey(slot, 0, role), item);
                    continue;
                }

                // Unresolved role — fall through to legacy ordinal keying (undiffable-but-present).
            }

            var ordinal = next.TryGetValue(item.Slot, out var n) ? n : 0;
            next[item.Slot] = ordinal + 1;
            Put(new SlotKey(item.Slot, ordinal), item);
        }

        warnings = warns;
        return new EquippedLoadout(map);
    }

    // Short user-facing label for a colliding item: its catalog type (with item power when known),
    // falling back to the slot name when the type is unresolved.
    private static string Describe(GearItem item)
    {
        var name = item.ItemTypeName ?? item.Slot.ToString();
        return item.ItemPower is { } power ? $"'{name}' ({power} Item Power)" : $"'{name}'";
    }
}
