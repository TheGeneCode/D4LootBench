using System.Runtime.CompilerServices;
using D4LootBench.Core.Data;
using D4LootBench.Core.Serialization;

namespace D4LootBench.Core.Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    public static void Init() => FilterDataContext.Set(new FilterDataService());
}
