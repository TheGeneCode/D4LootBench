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
    public void EquippedLoadout_nonbarb_twohand_keys_single_role()
    {
        var staff = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "Staff");

        var loadout = EquippedLoadout.FromItems([staff], PlayerClass.Sorcerer, ProgressionTestFactory.RoleMap());

        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.TwoHand));
        loadout.Items.ShouldNotContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand));
        loadout.Items.ShouldNotContainKey(new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand));
    }

    [Fact]
    public void EquippedLoadout_barb_dual_1h_maps_main_then_off()
    {
        var sword0 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "Sword");
        var sword1 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Sword");

        var loadout = EquippedLoadout.FromItems([sword0, sword1], PlayerClass.Barbarian, ProgressionTestFactory.RoleMap());

        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand));
        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand));
        loadout[new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand)].ShouldBe(sword0);
        loadout[new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand)].ShouldBe(sword1);
    }

    [Fact]
    public void FromItems_WeaponWithNullTypeFallsBackToOrdinal()
    {
        var weapon = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: null);

        var loadout = EquippedLoadout.FromItems([weapon], PlayerClass.Barbarian, ProgressionTestFactory.RoleMap());

        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0));
        loadout.Items.Keys.Single().Role.ShouldBe(WeaponSlotRole.None);
    }

    [Fact]
    public void FromItems_NullRoleWeapon_LegacyWhenNoRoleMap()
    {
        // With no role map (legacy callers), a typed weapon keys to a single roleless weapon slot.
        var twoHand = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "Two-Handed Sword");
        var polearm = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Polearm");

        var loadout = EquippedLoadout.FromItems([twoHand, polearm]);

        loadout.Items.Count.ShouldBe(1);
        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.None));
        loadout[new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.None)].ShouldBe(polearm); // last wins
    }

    [Fact]
    public void FromItems_KeysOffhandByRole()
    {
        // Offhand items with a resolved role key by that role.
        var focus = ProgressionTestFactory.Item(GearSlot.Offhand, [ProgressionTestFactory.Affix(1)], itemTypeName: "Focus");

        var loadout = EquippedLoadout.FromItems([focus], PlayerClass.Sorcerer, ProgressionTestFactory.RoleMap());

        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand));
        loadout[new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand)].ShouldBe(focus);
    }

    [Fact]
    public void FromItems_UnresolvedRoleWeapon_FallsBackToOrdinal()
    {
        // A weapon whose type does not classify to a role (nonsense name) falls through to ordinal keying.
        var untyped = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "zzqq nonsense");
        var sword = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Sword");

        var loadout = EquippedLoadout.FromItems([untyped, sword], PlayerClass.Barbarian, ProgressionTestFactory.RoleMap());

        loadout.Items.Count.ShouldBe(2);
        loadout[new SlotKey(GearSlot.Weapon, 0)].ShouldBe(untyped);
        loadout[new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand)].ShouldBe(sword);
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

    [Fact]
    public void EquippedLoadout_rogue_dual_1h_also_promotes_second_to_offhand()
    {
        // The dual-1H promotion in FromItems is not gated to PlayerClass.Barbarian in code — any class
        // whose second Mainhand-role item collides gets promoted to Offhand. Rogue dual-wields
        // Sword/Dagger, so this is a real (not just theoretical) case for a non-Barbarian class.
        var sword = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "Sword");
        var dagger = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Dagger");

        var loadout = EquippedLoadout.FromItems([sword, dagger], PlayerClass.Rogue, ProgressionTestFactory.RoleMap());

        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand));
        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand));
        loadout[new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand)].ShouldBe(sword);
        loadout[new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand)].ShouldBe(dagger);
    }

    [Fact]
    public void EquippedLoadout_thirdMainhandItem_silentlyOverwritesOffhandSlot()
    {
        // Three Mainhand-role items in one call (can't happen via real gear, but FromItems takes an
        // arbitrary IEnumerable): the 1st keys Mainhand, the 2nd promotes to Offhand — the 3rd ALSO fails
        // usedRoles.Add(Mainhand) and is promoted to the SAME Offhand slot key, silently overwriting the
        // 2nd item with no warning or exception. Documents current last-wins behavior, not a guard.
        var sword0 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "Sword");
        var sword1 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Sword");
        var sword2 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(3)], itemTypeName: "Sword");

        var loadout = EquippedLoadout.FromItems([sword0, sword1, sword2], PlayerClass.Barbarian, ProgressionTestFactory.RoleMap());

        loadout.Items.Count.ShouldBe(2);
        loadout[new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand)].ShouldBe(sword0);
        loadout[new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand)].ShouldBe(sword2); // 3rd silently wins over 2nd
    }

    [Fact]
    public void FromItems_NoCollision_EmitsNoWarnings()
    {
        var ring0 = ProgressionTestFactory.Item(GearSlot.Ring, [ProgressionTestFactory.Affix(1)]);
        var ring1 = ProgressionTestFactory.Item(GearSlot.Ring, [ProgressionTestFactory.Affix(2)]);

        _ = EquippedLoadout.FromItems([ring0, ring1], out var warnings);

        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void FromItems_DuplicateSlot_WarnsAndKeepsLast()
    {
        // Same physical weapon slot snipped twice with no role map: both typed weapons resolve to the
        // roleless (Weapon,0,None) key, so the first silently vanishes. Collision detection surfaces it.
        var first = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "Two-Handed Sword");
        var second = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Polearm");

        var loadout = EquippedLoadout.FromItems([first, second], out var warnings);

        loadout.Items.Count.ShouldBe(1);
        loadout[new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.None)].ShouldBe(second); // last wins
        var warning = warnings.ShouldHaveSingleItem();
        warning.ShouldContain("Polearm");         // kept
        warning.ShouldContain("Two-Handed Sword"); // dropped
    }

    [Fact]
    public void FromItems_DuplicateWeaponRole_WarnsAndKeepsLast()
    {
        // Two staves both classify as the Sorcerer's TwoHand role → same (Weapon,0,TwoHand) key; the
        // earlier staff is dropped last-wins. Collision detection reports the loss.
        var staff0 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "Staff");
        var staff1 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Staff");

        var loadout = EquippedLoadout.FromItems(
            [staff0, staff1], out var warnings, PlayerClass.Sorcerer, ProgressionTestFactory.RoleMap());

        loadout.Items.Count.ShouldBe(1);
        loadout[new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.TwoHand)].ShouldBe(staff1); // last wins
        warnings.ShouldHaveSingleItem().ShouldContain("TwoHand");
    }

    [Fact]
    public void FromItems_ThirdMainhand_PromotionCollision_Warns()
    {
        // Three Mainhand-role items: 1st→Mainhand, 2nd→Offhand (promoted), 3rd→ALSO Offhand, overwriting
        // the 2nd. Exactly one collision (the third), surfaced as a single warning.
        var sword0 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "Sword");
        var sword1 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Sword");
        var sword2 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(3)], itemTypeName: "Sword");

        var loadout = EquippedLoadout.FromItems(
            [sword0, sword1, sword2], out var warnings, PlayerClass.Barbarian, ProgressionTestFactory.RoleMap());

        loadout[new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand)].ShouldBe(sword2);
        warnings.ShouldHaveSingleItem().ShouldContain("Offhand");
    }

    [Fact]
    public void FromItems_DualWield_MainOffAssignmentIsInputOrderDependent()
    {
        // The main/off assignment for dual-1H is decided purely by input order: the FIRST Mainhand-role
        // item consumes the Mainhand slot and the SECOND is promoted to Offhand. D4 does not distinguish
        // the two hands for a dual-wielder, so this is accepted (not canonicalized) — reversing the input
        // simply swaps which physical item lands in which role. Pin the order-dependence so a future
        // "sort weapons before keying" change can't silently alter the assignment.
        var sword = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "Sword");
        var axe = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Axe");

        var forward = EquippedLoadout.FromItems([sword, axe], PlayerClass.Barbarian, ProgressionTestFactory.RoleMap());
        var reversed = EquippedLoadout.FromItems([axe, sword], PlayerClass.Barbarian, ProgressionTestFactory.RoleMap());

        forward[new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand)].ShouldBe(sword);
        forward[new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand)].ShouldBe(axe);

        reversed[new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand)].ShouldBe(axe);   // swapped
        reversed[new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand)].ShouldBe(sword); // swapped
    }

    [Fact]
    public void FromItems_PromotedMainhand_CollidesWithGenuineOffhand_OrderDependentLastWins()
    {
        // usedRoles only reserves the Mainhand role — it never reserves the Offhand slot. So a SECOND
        // Mainhand-role item promoted into (Offhand,0,Offhand) collides with a GENUINE offhand item that
        // keys to the same slot. Which one survives depends solely on input order (last-wins). This is a
        // hand-built impossibility in real gear (two 1H mains AND an offhand can't coexist), but FromItems
        // takes an arbitrary IEnumerable — pin the order-dependent collision + its warning.
        var dagger1 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(1)], itemTypeName: "Dagger");
        var dagger2 = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(2)], itemTypeName: "Dagger");
        var focus = ProgressionTestFactory.Item(GearSlot.Offhand, [ProgressionTestFactory.Affix(3)], itemTypeName: "Focus");
        var offhandKey = new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand);

        // Focus first: the promoted dagger2 overwrites it.
        var focusFirst = EquippedLoadout.FromItems(
            [focus, dagger1, dagger2], out var warnFocusFirst, PlayerClass.Warlock, ProgressionTestFactory.RoleMap());
        focusFirst[offhandKey].ShouldBe(dagger2);
        warnFocusFirst.ShouldHaveSingleItem().ShouldContain("Offhand");

        // Focus last: the genuine focus overwrites the promoted dagger2.
        var focusLast = EquippedLoadout.FromItems(
            [dagger1, dagger2, focus], out var warnFocusLast, PlayerClass.Warlock, ProgressionTestFactory.RoleMap());
        focusLast[offhandKey].ShouldBe(focus);
        warnFocusLast.ShouldHaveSingleItem().ShouldContain("Offhand");
    }

    [Fact]
    public void FromItems_LegacyNoRoleMap_OffhandSlotAlsoKeysRoleless()
    {
        // roleMap == null applies to BOTH GearSlot.Weapon and GearSlot.Offhand, not just Weapon.
        var focus = ProgressionTestFactory.Item(GearSlot.Offhand, [ProgressionTestFactory.Affix(1)], itemTypeName: "Focus");

        var loadout = EquippedLoadout.FromItems([focus]);

        loadout.Items.Count.ShouldBe(1);
        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.None));
        loadout[new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.None)].ShouldBe(focus);
    }
}
