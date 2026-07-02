namespace D4LootBench.Core.Tests.Progression;

using D4LootBench.Core.Gear;
using D4LootBench.Core.Progression;
using Shouldly;
using Xunit;

public class EquippedLoadoutTests
{
    [Fact]
    public void FromItems_AssignsOrdinals()
    {
        var ring0 = ProgressionTestFactory.Item(GearSlot.Ring, [ProgressionTestFactory.Affix(1)]);
        var ring1 = ProgressionTestFactory.Item(GearSlot.Ring, [ProgressionTestFactory.Affix(2)]);

        var loadout = EquippedLoadout.FromItems([ring0, ring1]);

        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Ring, 0));
        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Ring, 1));
        loadout[new SlotKey(GearSlot.Ring, 0)].ShouldBe(ring0);
        loadout[new SlotKey(GearSlot.Ring, 1)].ShouldBe(ring1);
    }

    [Fact]
    public void FromItems_KeysWeaponsByItemType()
    {
        var twoHand = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "Two-Handed Sword");
        var polearm = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Polearm");

        var loadout = EquippedLoadout.FromItems([twoHand, polearm]);

        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, "Two-Handed Sword"));
        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, "Polearm"));
        loadout[new SlotKey(GearSlot.Weapon, 0, "Two-Handed Sword")].ShouldBe(twoHand);
        loadout[new SlotKey(GearSlot.Weapon, 0, "Polearm")].ShouldBe(polearm);
    }

    [Fact]
    public void FromItems_SameTypeDualWieldCollapses()
    {
        var sword0 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "Sword");
        var sword1 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Sword");

        var loadout = EquippedLoadout.FromItems([sword0, sword1]);

        loadout.Items.Count.ShouldBe(1);
        loadout[new SlotKey(GearSlot.Weapon, 0, "Sword")].ShouldBe(sword1); // last wins
    }

    [Fact]
    public void FromItems_WeaponWithNullTypeFallsBackToOrdinal()
    {
        var weapon = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: null);

        var loadout = EquippedLoadout.FromItems([weapon]);

        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0));
        loadout.Items.Keys.Single().ItemType.ShouldBeNull();
    }

    [Fact]
    public void FromItems_KeysOffhandByItemType()
    {
        // Symmetric to weapons: Offhand items with a resolved ItemTypeName key by that concrete type too.
        var focus = ProgressionTestFactory.Item(GearSlot.Offhand, [ProgressionTestFactory.Affix(1)], itemTypeName: "Focus");
        var totem = ProgressionTestFactory.Item(GearSlot.Offhand, [ProgressionTestFactory.Affix(2)], itemTypeName: "Totem");

        var loadout = EquippedLoadout.FromItems([focus, totem]);

        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Offhand, 0, "Focus"));
        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Offhand, 0, "Totem"));
        loadout[new SlotKey(GearSlot.Offhand, 0, "Focus")].ShouldBe(focus);
    }

    [Fact]
    public void FromItems_NullAndTypedWeapon_CoexistAsDistinctKeys()
    {
        // A typed weapon never touches the ordinal counter, so a null-type weapon (OCR type-read
        // failure) keyed at ordinal 0 does NOT collide with a concrete-typed weapon also at ordinal 0 —
        // ItemType participates in SlotKey equality, so (Weapon,0,null) and (Weapon,0,"Sword") are
        // distinct dictionary entries that both survive.
        var untyped = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: null);
        var sword = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Sword");

        var loadout = EquippedLoadout.FromItems([untyped, sword]);

        loadout.Items.Count.ShouldBe(2);
        loadout[new SlotKey(GearSlot.Weapon, 0)].ShouldBe(untyped);
        loadout[new SlotKey(GearSlot.Weapon, 0, "Sword")].ShouldBe(sword);
    }

    [Fact]
    public void FromItems_TwoNullTypeWeapons_AssignIncrementingOrdinals()
    {
        // Both weapons fail OCR type resolution (ItemTypeName null) — they fall through to the ordinal
        // path and get distinct ordinals just like rings, rather than colliding on (Weapon,0,null).
        var weapon0 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: null);
        var weapon1 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: null);

        var loadout = EquippedLoadout.FromItems([weapon0, weapon1]);

        loadout.Items.Count.ShouldBe(2);
        loadout[new SlotKey(GearSlot.Weapon, 0)].ShouldBe(weapon0);
        loadout[new SlotKey(GearSlot.Weapon, 1)].ShouldBe(weapon1);
    }

    [Fact]
    public void FromItems_SkipsUnknownSlot()
    {
        var unknown = ProgressionTestFactory.Item(GearSlot.Unknown, [ProgressionTestFactory.Affix(1)]);

        var loadout = EquippedLoadout.FromItems([unknown]);

        loadout.Items.ShouldBeEmpty();
    }

    [Fact]
    public void Indexer_ReturnsNullForEmptySlot()
    {
        var loadout = EquippedLoadout.FromItems([]);

        loadout[new SlotKey(GearSlot.Helm)].ShouldBeNull();
    }
}
