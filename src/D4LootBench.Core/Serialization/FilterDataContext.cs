using D4LootBench.Core.Data;

namespace D4LootBench.Core.Serialization;

/// <summary>
/// Provides the <see cref="IFilterDataService"/> instance used by JSON converters that need
/// to resolve hash IDs to human-readable names. JSON converters are constructed by
/// <see cref="System.Text.Json.JsonSerializer"/> via parameterless attribute reflection, so
/// they cannot receive the service via constructor injection — this narrow context object
/// is the bridge.
///
/// Set once at application startup (App.xaml.cs OnStartup) and in test fixtures that
/// serialize annotated JSON. Setting twice is allowed for tests but the value should be
/// stable for the lifetime of the process.
/// </summary>
public static class FilterDataContext
{
    private static IFilterDataService? _current;

    public static IFilterDataService Current =>
        _current ?? throw new InvalidOperationException(
            "FilterDataContext.Current is not set. Call FilterDataContext.Set(IFilterDataService) " +
            "at application startup before serializing or deserializing filter JSON.");

    public static void Set(IFilterDataService service) => _current = service;
}
