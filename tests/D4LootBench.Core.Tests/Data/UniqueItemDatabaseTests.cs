using D4LootBench.Core.Data;
using Shouldly;

namespace D4LootBench.Core.Tests.Data;

/// <summary>Boundary coverage for <see cref="UniqueItemDatabase"/>'s class derivation, added alongside
/// the Flail class-list broadening (1HFlail/2HFlail hardcoded classes widened from Paladin-only to
/// Barbarian/Warlock/Necromancer/Paladin/Druid — see ProgressionFilterGenerator interchangeable-pools
/// handoff). Note for future maintainers: all three Flail uniques currently in the catalog carry an
/// explicit class token in their internal name (e.g. "1HFlail_Unique_Warlock_001"), so
/// <c>DeriveClasses</c>'s first pass — scanning every "_"-delimited segment for a known class name —
/// always resolves them before the broadened hardcoded-type-classes fallback is ever consulted. The
/// broadening therefore has no observable effect on any entry currently in d4-data.json; it only matters
/// for a hypothetical future Flail unique whose internal name carries no class segment (e.g. a
/// season/expansion-prefixed name). These tests lock down the current (already-correct) classification
/// via the public API rather than assert an effect the present data cannot exercise.</summary>
public sealed class UniqueItemDatabaseTests
{
    private static readonly string[] FlailUniqueNames = ["Scourge of Duriel", "Light's Rebuke", "Sunbrand"];

    [Fact]
    public void ForClass_Warlock_IncludesScourgeOfDuriel()
    {
        UniqueItemDatabase.ForClass("Warlock").ShouldContain(e => e.Name == "Scourge of Duriel");
    }

    [Theory]
    [InlineData("Light's Rebuke")]
    [InlineData("Sunbrand")]
    public void ForClass_Paladin_IncludesFlailUniques(string name)
    {
        UniqueItemDatabase.ForClass("Paladin").ShouldContain(e => e.Name == name);
    }

    [Theory]
    [InlineData("Rogue")]
    [InlineData("Sorcerer")]
    [InlineData("Spiritborn")]
    public void ForClass_ClassesOutsideFlailCatalogList_ExcludeAllFlailUniques(string className)
    {
        // Flail's d4-data.json entry lists classes = Barbarian/Warlock/Necromancer/Paladin/Druid only.
        UniqueItemDatabase.ForClass(className).ShouldNotContain(e => FlailUniqueNames.Contains(e.Name));
    }

    [Fact]
    public void BySnoId_FlailUniques_AreThreeDistinctEntries()
    {
        // Guards against a snoId/hash collision silently merging two of the three Flail uniques that
        // were added/reclassified around the same catalog change.
        var matched = UniqueItemDatabase.All.Where(e => FlailUniqueNames.Contains(e.Name)).ToList();

        matched.Count.ShouldBe(3);
        matched.Select(e => e.SnoId).Distinct().Count().ShouldBe(3);
        matched.ShouldAllBe(e => e.IsReleased);
    }
}
