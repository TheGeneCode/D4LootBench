namespace D4Loot.Core.Data;

public sealed record SkillEntry(string Name, uint Hash, string ClassName, bool InGameVerified);

public static class SkillDatabase
{
    public static IReadOnlyList<SkillEntry> All { get; }
    public static IReadOnlyDictionary<uint, SkillEntry> ByHash { get; }

    public static IReadOnlyList<SkillEntry> Generic => _byClass["All"];
    public static IReadOnlyList<SkillEntry> Barbarian => _byClass["Barbarian"];
    public static IReadOnlyList<SkillEntry> Druid => _byClass["Druid"];
    public static IReadOnlyList<SkillEntry> Necromancer => _byClass["Necromancer"];
    public static IReadOnlyList<SkillEntry> Rogue => _byClass["Rogue"];
    public static IReadOnlyList<SkillEntry> Sorcerer => _byClass["Sorcerer"];
    public static IReadOnlyList<SkillEntry> Spiritborn => _byClass["Spiritborn"];
    public static IReadOnlyList<SkillEntry> Paladin => _byClass["Paladin"];
    public static IReadOnlyList<SkillEntry> Warlock => _byClass["Warlock"];

    private static readonly Dictionary<string, List<SkillEntry>> _byClass;

    static SkillDatabase()
    {
        var all = new List<SkillEntry>();
        _byClass = new Dictionary<string, List<SkillEntry>>();

        var arr = FilterDataStore.Root.GetProperty("skills");
        foreach (var el in arr.EnumerateArray())
        {
            var name = el.GetProperty("displayName").GetString()!;
            var hashHex = el.GetProperty("hash").GetString()!;
            var hash = Convert.ToUInt32(hashHex[2..], 16);
            var cls = el.GetProperty("class").GetString()!;

            var entry = new SkillEntry(name, hash, cls, InGameVerified: false);
            all.Add(entry);

            if (!_byClass.TryGetValue(cls, out var list))
            {
                list = new List<SkillEntry>();
                _byClass[cls] = list;
            }
            list.Add(entry);
        }

        All = all.AsReadOnly();
        ByHash = all.Where(s => s.Hash != 0).ToDictionary(s => s.Hash);
    }

    public static string GetDisplayName(uint hash)
        => ByHash.TryGetValue(hash, out var entry) ? entry.Name : $"Unknown skill (0x{hash:x8})";
}
