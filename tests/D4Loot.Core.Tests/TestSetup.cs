using System.Runtime.CompilerServices;
using D4Loot.Core.Data;
using D4Loot.Core.Serialization;

namespace D4Loot.Core.Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    public static void Init() => FilterDataContext.Set(new FilterDataService());
}
