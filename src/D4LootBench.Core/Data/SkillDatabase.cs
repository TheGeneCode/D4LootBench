namespace D4LootBench.Core.Data;

public sealed record SkillEntry(string Name, uint Hash, IReadOnlyList<string> Classes, bool InGameVerified);

public static class SkillDatabase
{
    public static IReadOnlyList<SkillEntry> All { get; }
    public static IReadOnlyDictionary<uint, SkillEntry> ByHash { get; }

    private static readonly Dictionary<string, List<SkillEntry>> _byClass;

    static SkillDatabase()
    {
        var all = new List<SkillEntry>();
        _byClass = new Dictionary<string, List<SkillEntry>>();

        var arr = FilterDataStore.Root.GetProperty("skills");
        foreach (var el in arr.EnumerateArray())
        {
            var name    = el.GetProperty("displayName").GetString()!;
            var hashHex = el.GetProperty("hash").GetString()!;
            var hash    = Convert.ToUInt32(hashHex[2..], 16);
            var classes = el.GetProperty("classes")
                            .EnumerateArray()
                            .Select(c => c.GetString()!)
                            .ToList()
                            .AsReadOnly();

            var entry = new SkillEntry(name, hash, classes, InGameVerified: false);
            all.Add(entry);

            foreach (var cls in classes)
            {
                if (!_byClass.TryGetValue(cls, out var list))
                {
                    list = new List<SkillEntry>();
                    _byClass[cls] = list;
                }
                list.Add(entry);
            }
        }

        All    = all.AsReadOnly();
        ByHash = all.Where(s => s.Hash != 0).ToDictionary(s => s.Hash);
    }

    public static IReadOnlyList<SkillEntry> ForClass(string className)
        => _byClass.TryGetValue(className, out var list) ? list : [];

    public static string GetDisplayName(uint hash)
        => ByHash.TryGetValue(hash, out var entry) ? entry.Name : $"Unknown skill (0x{hash:x8})";
}
