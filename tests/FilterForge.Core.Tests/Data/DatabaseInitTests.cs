using ThunderEagle.FilterForge.Core.Data;

namespace ThunderEagle.FilterForge.Core.Tests.Data;

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
}
