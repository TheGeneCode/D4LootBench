namespace D4LootBench.Core.Profiles;

/// <summary>File-backed CRUD for <see cref="ProgressionProfile"/>s: one JSON file per profile,
/// named <c>{Id:N}.json</c>, under an injected root directory. Rename/Duplicate never touch
/// filenames other than the Id-derived one, so display names need no sanitization.</summary>
/// <param name="rootDirectory">The directory holding profile files (created on demand).</param>
/// <param name="clock">UTC clock seam; defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
public sealed class ProfileStore(string rootDirectory, Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly object _writeLock = new();

    private string PathFor(Guid id) => Path.Combine(rootDirectory, $"{id:N}.json");

    /// <summary>Enumerates every readable profile in the root directory, newest-modified first.</summary>
    /// <returns>The loaded profiles plus one warning per skipped (corrupt/unreadable) file.</returns>
    public ProfileLoadResult LoadAll()
    {
        if (!Directory.Exists(rootDirectory))
        {
            return new ProfileLoadResult();
        }

        var profiles = new List<ProgressionProfile>();
        var warnings = new List<string>();
        foreach (var path in Directory.EnumerateFiles(rootDirectory, "*.json"))
        {
            try
            {
                profiles.Add(ProfileSerializer.Deserialize(File.ReadAllText(path)));
            }
            catch (Exception ex)
            {
                warnings.Add($"Skipped unreadable profile file \"{Path.GetFileName(path)}\": {ex.Message}");
            }
        }

        return new ProfileLoadResult
        {
            Profiles = [.. profiles.OrderByDescending(p => p.ModifiedUtc)],
            Warnings = warnings,
        };
    }

    /// <summary>Loads a single profile by id.</summary>
    /// <param name="id">The profile identity.</param>
    /// <returns>The profile, or <c>null</c> when no file exists for the id.</returns>
    /// <exception cref="System.Text.Json.JsonException">The file exists but is unreadable.</exception>
    public ProgressionProfile? Load(Guid id)
    {
        var path = PathFor(id);
        return File.Exists(path) ? ProfileSerializer.Deserialize(File.ReadAllText(path)) : null;
    }

    /// <summary>Persists a profile, stamping <see cref="ProgressionProfile.ModifiedUtc"/> (and
    /// <see cref="ProgressionProfile.CreatedUtc"/> on first save). Writes via a temp file +
    /// atomic move so a crash mid-write cannot corrupt an existing file.</summary>
    /// <param name="profile">The profile to save; its <see cref="ProgressionProfile.Name"/> is trimmed.</param>
    /// <returns>The stamped profile as persisted.</returns>
    /// <exception cref="ArgumentException">The name is null or whitespace.</exception>
    public ProgressionProfile Save(ProgressionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new ArgumentException("Profile name must be non-empty.", nameof(profile));
        }

        var now = _clock();
        var stamped = profile with
        {
            Name = profile.Name.Trim(),
            ModifiedUtc = now,
            CreatedUtc = profile.CreatedUtc == default ? now : profile.CreatedUtc,
        };

        Directory.CreateDirectory(rootDirectory);
        var path = PathFor(profile.Id);
        var tmp = path + ".tmp";
        var json = ProfileSerializer.Serialize(stamped);

        // Serialize concurrent writers of the same id: two overlapping saves would otherwise
        // both target the shared "{id}.tmp" path, and whichever finishes File.Move first leaves
        // the other with a missing source file (IOException). A per-instance lock is sufficient
        // because this store is used as a single shared instance within the app process.
        lock (_writeLock)
        {
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }

        return stamped;
    }

    /// <summary>Deletes a profile file if it exists.</summary>
    /// <param name="id">The profile identity.</param>
    /// <returns><c>true</c> when a file was removed; <c>false</c> when none existed.</returns>
    public bool Delete(Guid id)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    /// <summary>Copies an existing profile under a new id and name.</summary>
    /// <param name="id">The source profile identity.</param>
    /// <param name="newName">The copy's name; when blank, a unique <c>"{name} (copy[ N])"</c> is generated.</param>
    /// <returns>The saved copy.</returns>
    /// <exception cref="InvalidOperationException">No source profile exists for the id.</exception>
    public ProgressionProfile Duplicate(Guid id, string? newName = null)
    {
        var source = Load(id) ?? throw new InvalidOperationException($"Profile {id:N} not found.");
        var copyName = string.IsNullOrWhiteSpace(newName)
            ? GenerateCopyName(source.Name)
            : newName.Trim();

        return Save(source with { Id = Guid.NewGuid(), Name = copyName, CreatedUtc = default });
    }

    /// <summary>Renames a profile in place, keeping its id and gear.</summary>
    /// <param name="id">The profile identity.</param>
    /// <param name="newName">The new display name (trimmed).</param>
    /// <returns>The renamed, re-stamped profile.</returns>
    /// <exception cref="ArgumentException">The name is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">No profile exists for the id.</exception>
    public ProgressionProfile Rename(Guid id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("Profile name must be non-empty.", nameof(newName));
        }

        var existing = Load(id) ?? throw new InvalidOperationException($"Profile {id:N} not found.");
        return Save(existing with { Name = newName.Trim() });
    }

    private string GenerateCopyName(string sourceName)
    {
        var existingNames = LoadAll().Profiles
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidate = $"{sourceName} (copy)";
        var counter = 2;
        while (existingNames.Contains(candidate))
        {
            candidate = $"{sourceName} (copy {counter})";
            counter++;
        }

        return candidate;
    }
}

/// <summary>Result of enumerating the profile directory: readable profiles plus warnings for skipped files.</summary>
public sealed record ProfileLoadResult
{
    /// <summary>Gets the successfully loaded profiles, newest-modified first.</summary>
    public IReadOnlyList<ProgressionProfile> Profiles { get; init; } = [];

    /// <summary>Gets one human-readable warning per skipped (corrupt/unreadable) file.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
