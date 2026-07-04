using D4LootBench.Core.Data;

namespace D4LootBench.Core.Tests.Data;

public sealed class DatabaseInitTests
{
    [Fact]
    public void ItemTypeDatabase_InitializesWithoutThrowing()
    {
        var ex = Record.Exception(() => { var _ = ItemTypeDatabase.All.Count; });
        if (ex is TypeInitializationException tie)
            throw tie.InnerException ?? tie;
        Assert.Null(ex);
    }

    [Fact]
    public void SkillDatabase_InitializesWithoutThrowing()
    {
        var ex = Record.Exception(() => { var _ = SkillDatabase.All.Count; });
        if (ex is TypeInitializationException tie)
            throw tie.InnerException ?? tie;
        Assert.Null(ex);
    }

    [Fact]
    public void AffixDatabase_InitializesWithoutThrowing()
    {
        var ex = Record.Exception(() => { var _ = AffixDatabase.All.Count; });
        if (ex is TypeInitializationException tie)
            throw tie.InnerException ?? tie;
        Assert.Null(ex);
    }

    [Fact]
    public void UniqueItemDatabase_InitializesWithoutThrowing()
    {
        // No prior test in the suite touched UniqueItemDatabase's static ctor directly (only indirectly
        // via FilterCodecTests) — a gap given DeriveClasses/HardcodedTypeClasses were recently edited.
        var ex = Record.Exception(() => { var _ = UniqueItemDatabase.All.Count; });
        if (ex is TypeInitializationException tie)
            throw tie.InnerException ?? tie;
        Assert.Null(ex);
    }
}
