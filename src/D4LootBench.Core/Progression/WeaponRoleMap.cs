namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;

/// <summary>Maps between a class-specific <see cref="WeaponSlotRole"/> and the concrete item-type
/// hashes that role admits, derived from the item-type catalog. Instance class so a shared
/// <see cref="NameResolver"/> resolves guide/equipped item-type display names to hashes.</summary>
/// <param name="resolver">The shared fuzzy name resolver.</param>
public sealed class WeaponRoleMap(NameResolver resolver)
{
    // Catalog internal name for the Totem is "OffHandTotem" (not "Totem"); Shield lives in the Armor
    // category but plays an offhand role for Necromancer/Paladin.
    private static readonly HashSet<string> OffhandInternal =
        new(StringComparer.Ordinal) { "Focus", "OffHandTotem", "Shield" };

    private static readonly HashSet<string> SlicingInternal =
        new(StringComparer.Ordinal) { "Polearm", "Sword2H", "Axe2H" };

    private static readonly HashSet<string> BludgeoningInternal =
        new(StringComparer.Ordinal) { "Mace2H" };

    /// <summary>Every item-type hash allowed in a slot playing <paramref name="role"/> for
    /// <paramref name="cls"/>. Empty when the role is meaningless for the class. Ordered by hash
    /// ascending for deterministic <c>ItemTypeCondition.TypeIds</c> / golden snapshots.</summary>
    /// <param name="role">The weapon slot role to expand.</param>
    /// <param name="cls">The player class (<see cref="PlayerClass.All"/> = class-agnostic union).</param>
    /// <returns>The allowed item-type hashes, ascending.</returns>
    public IReadOnlyList<uint> AllowedTypeHashes(WeaponSlotRole role, PlayerClass cls)
    {
        var className = ClassName(cls);

        IEnumerable<ItemTypeEntry> Matches(Func<ItemTypeEntry, bool> predicate) =>
            ItemTypeDatabase.All.Where(e => predicate(e) && ClassMatch(e, className));

        IEnumerable<ItemTypeEntry> selected = role switch
        {
            // Bludgeoning/Slicing are fixed catalog sets — class filter is a no-op.
            WeaponSlotRole.Bludgeoning => ItemTypeDatabase.All.Where(e => BludgeoningInternal.Contains(e.InternalName)),
            WeaponSlotRole.Slicing => ItemTypeDatabase.All.Where(e => SlicingInternal.Contains(e.InternalName)),
            WeaponSlotRole.Mainhand => Matches(IsOneHandedWeapon),
            WeaponSlotRole.TwoHand => Matches(e => IsWeapon(e) && IsTwoHanded(e)),
            WeaponSlotRole.Offhand => cls == PlayerClass.Barbarian
                ? Matches(IsOneHandedWeapon)
                : Matches(e => IsOneHandedWeapon(e) || OffhandInternal.Contains(e.InternalName)),
            _ => [],
        };

        return selected.Select(e => e.Hash).Distinct().OrderBy(h => h).ToList();
    }

    /// <summary>Classifies a concrete item-type display name into the role its equipped/guide item
    /// plays for <paramref name="cls"/>. Returns <see cref="WeaponSlotRole.None"/> when the name does
    /// not resolve to a weapon/offhand type.</summary>
    /// <param name="itemTypeName">The item-type display name (e.g. "Two-Handed Mace").</param>
    /// <param name="cls">The player class.</param>
    /// <returns>The role the item plays, or <see cref="WeaponSlotRole.None"/>.</returns>
    public WeaponSlotRole RoleForItemType(string itemTypeName, PlayerClass cls)
    {
        if (!resolver.TryResolveItemType(itemTypeName, out var hash, out _) ||
            !ItemTypeDatabase.ByHash.TryGetValue(hash, out var entry) ||
            !(IsWeapon(entry) || entry.InternalName == "Shield"))
        {
            return WeaponSlotRole.None;
        }

        if (cls == PlayerClass.Barbarian)
        {
            if (BludgeoningInternal.Contains(entry.InternalName))
            {
                return WeaponSlotRole.Bludgeoning;
            }

            if (SlicingInternal.Contains(entry.InternalName))
            {
                return WeaponSlotRole.Slicing;
            }

            if (IsTwoHanded(entry))
            {
                return WeaponSlotRole.TwoHand;
            }

            return OffhandInternal.Contains(entry.InternalName) ? WeaponSlotRole.Offhand : WeaponSlotRole.Mainhand;
        }

        if (OffhandInternal.Contains(entry.InternalName))
        {
            return WeaponSlotRole.Offhand;
        }

        return IsTwoHanded(entry) ? WeaponSlotRole.TwoHand : WeaponSlotRole.Mainhand;
    }

    // Two-handed weapons whose internal name lacks the "2H" suffix (Staff/Polearm/Bow are inherently 2H).
    private static readonly HashSet<string> TwoHandedIrregular =
        new(StringComparer.Ordinal) { "Staff", "Polearm", "Bow" };

    private static bool IsTwoHanded(ItemTypeEntry e) =>
        e.InternalName.EndsWith("2H", StringComparison.Ordinal) || TwoHandedIrregular.Contains(e.InternalName);

    private static bool IsWeapon(ItemTypeEntry e) => e.Category == "Weapons";

    private static bool IsOneHandedWeapon(ItemTypeEntry e) =>
        IsWeapon(e) && !IsTwoHanded(e) && !OffhandInternal.Contains(e.InternalName);

    private static bool ClassMatch(ItemTypeEntry e, string? className) =>
        className is null || e.Classes.Contains(className) || e.Classes.Contains("All");

    private static string? ClassName(PlayerClass cls) => cls == PlayerClass.All ? null : cls.ToString();
}
