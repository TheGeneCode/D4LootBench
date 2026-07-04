namespace D4LootBench.Core.Tests.Progression;

using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Progression;
using Shouldly;

public sealed class WeaponRoleMapTests
{
    private static readonly NameResolver Resolver = new(new FilterDataService());
    private static readonly WeaponRoleMap Map = new(Resolver);

    private static uint Hash(string typeName)
    {
        Resolver.TryResolveItemType(typeName, out var hash, out _).ShouldBeTrue($"{typeName} should resolve");
        return hash;
    }

    [Fact]
    public void WeaponRoleMap_barb_slicing_hashes()
    {
        var hashes = Map.AllowedTypeHashes(WeaponSlotRole.Slicing, PlayerClass.Barbarian);

        hashes.ShouldContain(Hash("Polearm"));
        hashes.ShouldContain(Hash("Two-Handed Sword"));
        hashes.ShouldContain(Hash("Two-Handed Axe"));
        hashes.ShouldNotContain(Hash("Two-Handed Mace"));
    }

    [Fact]
    public void WeaponRoleMap_barb_bludgeoning_is_2h_mace()
    {
        var hashes = Map.AllowedTypeHashes(WeaponSlotRole.Bludgeoning, PlayerClass.Barbarian);

        hashes.ShouldHaveSingleItem();
        hashes.Single().ShouldBe(Hash("Two-Handed Mace"));
    }

    [Fact]
    public void WeaponRoleMap_roleForItemType_barb()
    {
        Map.RoleForItemType("Two-Handed Mace", PlayerClass.Barbarian).ShouldBe(WeaponSlotRole.Bludgeoning);
        Map.RoleForItemType("Polearm", PlayerClass.Barbarian).ShouldBe(WeaponSlotRole.Slicing);
        Map.RoleForItemType("Sword", PlayerClass.Barbarian).ShouldBe(WeaponSlotRole.Mainhand);
    }

    [Fact]
    public void WeaponRoleMap_nonbarb_twohand_and_offhand()
    {
        Map.RoleForItemType("Staff", PlayerClass.Sorcerer).ShouldBe(WeaponSlotRole.TwoHand);
        Map.RoleForItemType("Focus", PlayerClass.Sorcerer).ShouldBe(WeaponSlotRole.Offhand);
        Map.RoleForItemType("Wand", PlayerClass.Sorcerer).ShouldBe(WeaponSlotRole.Mainhand);
    }

    [Fact]
    public void WeaponRoleMap_hashes_ordered_ascending()
    {
        var hashes = Map.AllowedTypeHashes(WeaponSlotRole.Slicing, PlayerClass.Barbarian);

        hashes.ShouldBe(hashes.OrderBy(h => h).ToList());
    }

    [Fact]
    public void WeaponRoleMap_nonweapon_name_is_none()
    {
        Map.RoleForItemType("Helm", PlayerClass.Barbarian).ShouldBe(WeaponSlotRole.None);
        Map.RoleForItemType("zzqq nonsense", PlayerClass.Barbarian).ShouldBe(WeaponSlotRole.None);
    }

    // --- Rogue: Bow is a two-handed irregular (no "2H" suffix); Crossbow/Crossbow2H is the regular pair ---

    [Fact]
    public void WeaponRoleMap_rogue_bow_is_twohand_irregular()
    {
        Map.RoleForItemType("Bow", PlayerClass.Rogue).ShouldBe(WeaponSlotRole.TwoHand);
        Map.AllowedTypeHashes(WeaponSlotRole.TwoHand, PlayerClass.Rogue).ShouldContain(Hash("Bow"));
    }

    [Fact]
    public void WeaponRoleMap_rogue_crossbow_pair_1h_vs_2h()
    {
        // Internal "Crossbow" (displayName "Hand Crossbow") is 1H; internal "Crossbow2H" (displayName
        // "Crossbow") carries the regular "2H" suffix.
        Map.RoleForItemType("Hand Crossbow", PlayerClass.Rogue).ShouldBe(WeaponSlotRole.Mainhand);
        Map.RoleForItemType("Crossbow", PlayerClass.Rogue).ShouldBe(WeaponSlotRole.TwoHand);
    }

    // --- Druid: Polearm is a two-handed irregular for a NON-Barbarian class (must resolve TwoHand, not
    // Slicing — the Bludgeoning/Slicing split is Barbarian-only); Totem (internal "OffHandTotem") offhand ---

    [Fact]
    public void WeaponRoleMap_druid_polearm_is_twohand_not_slicing()
    {
        Map.RoleForItemType("Polearm", PlayerClass.Druid).ShouldBe(WeaponSlotRole.TwoHand);
    }

    [Fact]
    public void WeaponRoleMap_druid_totem_is_offhand()
    {
        Map.RoleForItemType("Totem", PlayerClass.Druid).ShouldBe(WeaponSlotRole.Offhand);
        Map.AllowedTypeHashes(WeaponSlotRole.Offhand, PlayerClass.Druid).ShouldContain(Hash("Totem"));
    }

    // --- Necromancer: Scythe/Two-Handed Scythe pair (regular "2H" suffix) + Shield offhand ---

    [Fact]
    public void WeaponRoleMap_necromancer_scythe_pair_1h_vs_2h()
    {
        Map.RoleForItemType("Scythe", PlayerClass.Necromancer).ShouldBe(WeaponSlotRole.Mainhand);
        Map.RoleForItemType("Two-Handed Scythe", PlayerClass.Necromancer).ShouldBe(WeaponSlotRole.TwoHand);
    }

    [Fact]
    public void WeaponRoleMap_necromancer_shield_is_offhand()
    {
        // Shield lives in the Armor category (not Weapons) but is special-cased in RoleForItemType.
        Map.RoleForItemType("Shield", PlayerClass.Necromancer).ShouldBe(WeaponSlotRole.Offhand);
    }

    // --- Warlock: has BOTH a one-handed weapon (Dagger) and a true offhand (Focus) ---

    [Fact]
    public void WeaponRoleMap_warlock_dagger_mainhand_and_focus_offhand()
    {
        Map.RoleForItemType("Dagger", PlayerClass.Warlock).ShouldBe(WeaponSlotRole.Mainhand);
        Map.RoleForItemType("Focus", PlayerClass.Warlock).ShouldBe(WeaponSlotRole.Offhand);
    }

    // --- Spiritborn: zero catalog weapon entries for any role (data gap, not a crash) ---

    [Theory]
    [InlineData(WeaponSlotRole.Mainhand)]
    [InlineData(WeaponSlotRole.Offhand)]
    [InlineData(WeaponSlotRole.TwoHand)]
    public void WeaponRoleMap_spiritborn_has_no_catalog_weapons_for_role(WeaponSlotRole role)
    {
        Map.AllowedTypeHashes(role, PlayerClass.Spiritborn).ShouldBeEmpty();
    }

    [Fact]
    public void WeaponRoleMap_bludgeoning_slicing_class_filter_is_a_documented_noop()
    {
        // Bludgeoning/Slicing are fixed catalog sets that ignore the class parameter entirely (per the
        // implementation comment) — even for a class that could never equip them (Spiritborn) or a class
        // with no Barbarian-style split at all (Sorcerer), the same fixed set comes back.
        var barbarianSet = Map.AllowedTypeHashes(WeaponSlotRole.Bludgeoning, PlayerClass.Barbarian);
        var sorcererSet = Map.AllowedTypeHashes(WeaponSlotRole.Bludgeoning, PlayerClass.Sorcerer);
        var spiritbornSet = Map.AllowedTypeHashes(WeaponSlotRole.Bludgeoning, PlayerClass.Spiritborn);

        sorcererSet.ShouldBe(barbarianSet);
        spiritbornSet.ShouldBe(barbarianSet);
    }

    [Fact]
    public void WeaponRoleMap_roleForItemType_does_not_validate_class_catalog_membership()
    {
        // RoleForItemType classifies purely from the resolved ItemTypeEntry's shape (weapon? two-handed?
        // offhand-internal?) — unlike AllowedTypeHashes, it never checks entry.Classes. A Spiritborn (who
        // has ZERO catalog weapon entries of its own) still gets a concrete classification for "Sword"
        // because the Sword entry exists globally in the catalog for other classes. Documents current
        // permissive behavior rather than asserting a class guard that does not exist.
        Map.RoleForItemType("Sword", PlayerClass.Spiritborn).ShouldBe(WeaponSlotRole.Mainhand);

        // But AllowedTypeHashes(Mainhand, Spiritborn) — used by the generator to build the item-type gate
        // — correctly comes back empty, since it DOES apply ClassMatch. The two methods disagree on
        // whether Spiritborn "has" a Sword; callers must not assume RoleForItemType implies the type is
        // actually reachable via AllowedTypeHashes for the same class.
        Map.AllowedTypeHashes(WeaponSlotRole.Mainhand, PlayerClass.Spiritborn).ShouldBeEmpty();
    }

    [Fact]
    public void WeaponRoleMap_barbarian_shield_classified_offhand_but_excluded_from_allowedhashes()
    {
        // Shield's catalog Classes list is {Necromancer, Paladin} — a Barbarian can never equip one in
        // game. RoleForItemType still classifies it Offhand (no class check, see above), but Barbarian's
        // AllowedTypeHashes(Offhand, ...) branch only expands to one-handed weapons (dual-wield), so
        // Shield's hash never appears there. A hand-built EquippedLoadout containing an (impossible)
        // Barbarian Shield would produce a rule whose item-type gate does not actually match it.
        Map.RoleForItemType("Shield", PlayerClass.Barbarian).ShouldBe(WeaponSlotRole.Offhand);
        Map.AllowedTypeHashes(WeaponSlotRole.Offhand, PlayerClass.Barbarian).ShouldNotContain(Hash("Shield"));
    }

    [Fact]
    public void WeaponRoleMap_barbarian_offhand_hashes_equal_mainhand_hashes()
    {
        // Barbarian's Offhand role is "a second one-handed weapon", so its allowed set should be
        // identical to Mainhand's (both = every one-handed weapon in the Barbarian's class list).
        var offhand = Map.AllowedTypeHashes(WeaponSlotRole.Offhand, PlayerClass.Barbarian);
        var mainhand = Map.AllowedTypeHashes(WeaponSlotRole.Mainhand, PlayerClass.Barbarian);

        offhand.ShouldBe(mainhand);
        offhand.ShouldNotBeEmpty();
    }

    [Fact]
    public void WeaponRoleMap_barbarian_1h_includes_flail()
    {
        Map.AllowedTypeHashes(WeaponSlotRole.Mainhand, PlayerClass.Barbarian).ShouldContain(Hash("Flail"));
        Map.AllowedTypeHashes(WeaponSlotRole.Offhand, PlayerClass.Barbarian).ShouldContain(Hash("Flail"));
    }

    [Fact]
    public void WeaponRoleMap_roleForItemType_flail_is_mainhand()
    {
        // Complements WeaponRoleMap_barbarian_1h_includes_flail (which tests the role→hash-set direction)
        // with the concrete-item→role direction: an equipped Flail must classify as Mainhand for a
        // Barbarian (one-handed, not Bludgeoning/Slicing/two-handed/offhand-internal).
        Map.RoleForItemType("Flail", PlayerClass.Barbarian).ShouldBe(WeaponSlotRole.Mainhand);
    }

    [Theory]
    [InlineData(PlayerClass.Rogue)]
    [InlineData(PlayerClass.Sorcerer)]
    [InlineData(PlayerClass.Spiritborn)]
    public void WeaponRoleMap_flail_excluded_for_classes_outside_catalog_list(PlayerClass cls)
    {
        // d4-data.json's Flail entry lists classes = Barbarian/Warlock/Necromancer/Paladin/Druid only —
        // Rogue/Sorcerer/Spiritborn must never see Flail in their Mainhand gate.
        Map.AllowedTypeHashes(WeaponSlotRole.Mainhand, cls).ShouldNotContain(Hash("Flail"));
    }

    [Fact]
    public void WeaponRoleMap_mainhand_allclass_is_union_across_classes()
    {
        // PlayerClass.All => ClassName returns null => ClassMatch is unconditionally true, so the result
        // is the class-agnostic union of every one-handed weapon type across all classes.
        var union = Map.AllowedTypeHashes(WeaponSlotRole.Mainhand, PlayerClass.All);

        union.ShouldContain(Hash("Sword"));   // shared by most classes
        union.ShouldContain(Hash("Dagger"));  // Rogue/Warlock/Necromancer/Sorcerer/Druid
        union.ShouldContain(Hash("Wand"));    // Necromancer/Sorcerer only
    }
}
