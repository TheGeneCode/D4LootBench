using System.Runtime.CompilerServices;
using ThunderEagle.FilterForge.Core.Data;
using ThunderEagle.FilterForge.Core.Serialization;

namespace ThunderEagle.FilterForge.Core.Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    public static void Init() => FilterDataContext.Set(new FilterDataService());
}
