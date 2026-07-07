using System.Collections.Concurrent;
using System.Text.Json;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;
using D4LootBench.Core.Profiles;
using Shouldly;

namespace D4LootBench.Core.Tests.Profiles;

public sealed class ProfileStoreTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "d4lb-profile-tests", Guid.NewGuid().ToString("N"));
    private DateTimeOffset _now = T0;
    private readonly ProfileStore _store;

    public ProfileStoreTests() => _store = new ProfileStore(_dir, () => _now);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; a locked temp file must not fail the test run.
        }
    }

    private static ProgressionProfile Sample(string name = "Barb Bash") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        PlayerClass = PlayerClass.Barbarian,
        GuideFormat = BuildGuideFormat.Maxroll,
        GuideText = "Helm\nCritical Strike Chance\n",
        Gear =
        [
            new GearItem
            {
                Slot = GearSlot.Helm,
                ItemTypeName = "Helm",
                ItemPower = 925,
                Rarity = ItemRarity.Legendary,
                IsAncestral = true,
                UniqueHash = 0x00abc123,
                Affixes =
                [
                    new GearAffix { RawText = "+45.0% Critical Strike Chance", ResolvedName = "Critical Strike Chance", AffixHash = 0x001d5d01, IsGreaterAffix = true },
                    new GearAffix { RawText = "garbled ocr line", ResolvedName = null, AffixHash = null, IsGreaterAffix = false },
                ],
            },
        ],
    };

    [Fact]
    public void SaveThenLoadAll_RoundTrips()
    {
        _store.Save(Sample());

        var result = _store.LoadAll();

        result.Profiles.Count.ShouldBe(1);
        result.Warnings.ShouldBeEmpty();
        result.Profiles[0].Name.ShouldBe("Barb Bash");
        result.Profiles[0].Gear.Count.ShouldBe(1);
        result.Profiles[0].Gear[0].Affixes.Count.ShouldBe(2);
    }

    [Fact]
    public void Save_BlankName_Throws()
        => Should.Throw<ArgumentException>(() => _store.Save(Sample(name: "  ")));

    [Fact]
    public void Save_StampsTimestamps()
    {
        var first = _store.Save(Sample());
        first.CreatedUtc.ShouldBe(T0);
        first.ModifiedUtc.ShouldBe(T0);

        _now = T0.AddHours(1);
        var second = _store.Save(first);
        second.CreatedUtc.ShouldBe(T0);
        second.ModifiedUtc.ShouldBe(T0.AddHours(1));
    }

    [Fact]
    public void LoadAll_MissingDirectory_ReturnsEmpty()
    {
        var result = _store.LoadAll();

        result.Profiles.ShouldBeEmpty();
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void LoadAll_CorruptFile_SkippedWithWarning()
    {
        _store.Save(Sample());
        File.WriteAllText(Path.Combine(_dir, "bad.json"), "{oops");

        var result = _store.LoadAll();

        result.Profiles.Count.ShouldBe(1);
        result.Warnings.Count.ShouldBe(1);
        result.Warnings[0].ShouldContain("bad.json");
    }

    [Fact]
    public void LoadAll_SortsByModifiedDescending()
    {
        _store.Save(Sample("A"));
        _now = T0.AddHours(1);
        _store.Save(Sample("B"));

        var result = _store.LoadAll();

        result.Profiles[0].Name.ShouldBe("B");
    }

    [Fact]
    public void Delete_ExistingProfile_ReturnsTrueAndRemovesFile()
    {
        var saved = _store.Save(Sample());

        _store.Delete(saved.Id).ShouldBeTrue();
        _store.LoadAll().Profiles.ShouldBeEmpty();
    }

    [Fact]
    public void Delete_MissingId_ReturnsFalse()
        => _store.Delete(Guid.NewGuid()).ShouldBeFalse();

    [Fact]
    public void Duplicate_CreatesNewIdWithCopyName()
    {
        var saved = _store.Save(Sample());
        _now = T0.AddHours(1);

        var copy = _store.Duplicate(saved.Id);

        copy.Id.ShouldNotBe(saved.Id);
        copy.Name.ShouldBe("Barb Bash (copy)");
        copy.CreatedUtc.ShouldBe(T0.AddHours(1));
        copy.Gear.Count.ShouldBe(1);
        copy.Gear[0].UniqueHash.ShouldBe(0x00abc123u);
        copy.Gear[0].Affixes.Count.ShouldBe(2);
    }

    [Fact]
    public void Duplicate_NameCollision_Increments()
    {
        var saved = _store.Save(Sample());

        _store.Duplicate(saved.Id);
        var second = _store.Duplicate(saved.Id);

        second.Name.ShouldBe("Barb Bash (copy 2)");
    }

    [Fact]
    public void Duplicate_MissingId_Throws()
        => Should.Throw<InvalidOperationException>(() => _store.Duplicate(Guid.NewGuid()));

    [Fact]
    public void Rename_ChangesNameKeepsIdAndGear()
    {
        var saved = _store.Save(Sample());
        _now = T0.AddHours(1);

        var renamed = _store.Rename(saved.Id, "New Name");

        renamed.Id.ShouldBe(saved.Id);
        renamed.ModifiedUtc.ShouldBe(T0.AddHours(1));
        renamed.Gear.Count.ShouldBe(saved.Gear.Count);
        _store.Load(saved.Id)!.Name.ShouldBe("New Name");
    }

    [Fact]
    public void Rename_BlankName_Throws()
    {
        var saved = _store.Save(Sample());

        Should.Throw<ArgumentException>(() => _store.Rename(saved.Id, " "));
    }

    [Fact]
    public void Save_NameWithLeadingTrailingWhitespace_TrimsName()
    {
        var saved = _store.Save(Sample(name: "  Barb Bash  "));

        saved.Name.ShouldBe("Barb Bash");
        _store.Load(saved.Id)!.Name.ShouldBe("Barb Bash");
    }

    [Fact]
    public void Save_NullName_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() => _store.Save(Sample(name: null!)));

    [Fact]
    public void Save_EmptyGuidId_SavesAndLoadsSuccessfully()
    {
        var saved = _store.Save(Sample() with { Id = Guid.Empty });

        saved.Id.ShouldBe(Guid.Empty);
        _store.Load(Guid.Empty)!.Name.ShouldBe("Barb Bash");
    }

    [Fact]
    public void Save_VeryLongUnicodeName_RoundTrips()
    {
        var longName = string.Concat(Enumerable.Repeat("日本語テスト-", 500));

        var saved = _store.Save(Sample(name: longName));

        _store.Load(saved.Id)!.Name.ShouldBe(longName);
    }

    [Fact]
    public void Save_ConcurrentDifferentIds_AllPersistWithoutError()
    {
        var ids = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToArray();

        Parallel.ForEach(ids, id => _store.Save(Sample($"Profile {id:N}") with { Id = id }));

        var result = _store.LoadAll();
        result.Warnings.ShouldBeEmpty();
        result.Profiles.Count.ShouldBe(ids.Length);
        foreach (var id in ids)
        {
            _store.Load(id).ShouldNotBeNull();
        }
    }

    [Fact]
    public void Save_ConcurrentSameId_NoExceptionsAndFinalStateIsValid()
    {
        // Regression test: overlapping saves to the same id both wrote to a shared "{id}.tmp"
        // path; whichever finished File.Move first left the other with a missing source file
        // (IOException). ProfileStore.Save now serializes writers with a per-instance lock.
        var id = Guid.NewGuid();
        var exceptions = new ConcurrentBag<Exception>();

        Parallel.For(0, 50, i =>
        {
            try
            {
                _store.Save(Sample($"Race {i}") with { Id = id });
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        exceptions.ShouldBeEmpty();
        var loaded = _store.Load(id);
        loaded.ShouldNotBeNull();
        loaded!.Name.ShouldStartWith("Race ");
    }

    [Fact]
    public void Load_MissingDirectory_ReturnsNull()
        => _store.Load(Guid.NewGuid()).ShouldBeNull();

    [Fact]
    public void Load_CorruptExistingFile_ThrowsJsonException()
    {
        var saved = _store.Save(Sample());
        File.WriteAllText(Path.Combine(_dir, $"{saved.Id:N}.json"), "{oops");

        Should.Throw<JsonException>(() => _store.Load(saved.Id));
    }

    [Fact]
    public void LoadAll_EmptyDirectoryNoFiles_ReturnsEmpty()
    {
        Directory.CreateDirectory(_dir);

        var result = _store.LoadAll();

        result.Profiles.ShouldBeEmpty();
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void LoadAll_IgnoresNonJsonFiles()
    {
        _store.Save(Sample());
        File.WriteAllText(Path.Combine(_dir, "notes.txt"), "not a profile");
        File.WriteAllText(Path.Combine(_dir, "leftover.json.tmp"), "{stale tmp from a crashed save");

        var result = _store.LoadAll();

        result.Profiles.Count.ShouldBe(1);
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void LoadAll_MultipleCorruptFiles_OneWarningEach()
    {
        _store.Save(Sample());
        File.WriteAllText(Path.Combine(_dir, "bad1.json"), "{oops");
        File.WriteAllText(Path.Combine(_dir, "bad2.json"), "{also oops");

        var result = _store.LoadAll();

        result.Profiles.Count.ShouldBe(1);
        result.Warnings.Count.ShouldBe(2);
    }

    [Fact]
    public void LoadAll_EqualModifiedUtc_BothProfilesPresent()
    {
        _store.Save(Sample("A"));
        _store.Save(Sample("B"));

        var result = _store.LoadAll();

        result.Profiles.Count.ShouldBe(2);
        result.Profiles.Select(p => p.Name).ShouldBe(["A", "B"], ignoreOrder: true);
    }

    [Fact]
    public void Delete_CalledTwice_SecondReturnsFalse()
    {
        var saved = _store.Save(Sample());

        _store.Delete(saved.Id).ShouldBeTrue();
        _store.Delete(saved.Id).ShouldBeFalse();
    }

    [Fact]
    public void Duplicate_ChainedDuplicates_IncrementsThroughCopyThree()
    {
        var saved = _store.Save(Sample());

        _store.Duplicate(saved.Id);
        _store.Duplicate(saved.Id);
        var third = _store.Duplicate(saved.Id);

        third.Name.ShouldBe("Barb Bash (copy 3)");
    }

    [Fact]
    public void Duplicate_CaseInsensitiveNameCollision_Increments()
    {
        var saved = _store.Save(Sample());
        _store.Save(Sample("barb bash (copy)"));

        var copy = _store.Duplicate(saved.Id);

        copy.Name.ShouldBe("Barb Bash (copy 2)");
    }

    [Fact]
    public void Duplicate_WhitespaceOnlyNewName_GeneratesCopyNameInstead()
    {
        var saved = _store.Save(Sample());

        var copy = _store.Duplicate(saved.Id, newName: "   ");

        copy.Name.ShouldBe("Barb Bash (copy)");
    }

    [Fact]
    public void Duplicate_ExplicitNameCollidingWithExisting_AllowsDuplicateNames()
    {
        // By design: an explicit newName is used verbatim, with no collision check
        // (only the auto-generated "(copy[ N])" name avoids collisions).
        var saved = _store.Save(Sample());

        var copy = _store.Duplicate(saved.Id, newName: "Barb Bash");

        copy.Name.ShouldBe("Barb Bash");
        copy.Id.ShouldNotBe(saved.Id);
        _store.LoadAll().Profiles.Count.ShouldBe(2);
    }

    [Fact]
    public void Duplicate_WithCorruptFileInDirectory_StillSucceeds()
    {
        var saved = _store.Save(Sample());
        File.WriteAllText(Path.Combine(_dir, "bad.json"), "{oops");

        var copy = _store.Duplicate(saved.Id);

        copy.Name.ShouldBe("Barb Bash (copy)");
    }

    [Fact]
    public void Rename_ToNameOfAnotherExistingProfile_AllowsDuplicateNames()
    {
        var first = _store.Save(Sample("First"));
        var second = _store.Save(Sample("Second"));

        var renamed = _store.Rename(second.Id, "First");

        renamed.Name.ShouldBe("First");
        _store.Load(first.Id)!.Name.ShouldBe("First");
    }

    [Fact]
    public void Rename_TrimsWhitespaceFromNewName()
    {
        var saved = _store.Save(Sample());

        var renamed = _store.Rename(saved.Id, "  New Name  ");

        renamed.Name.ShouldBe("New Name");
    }

    [Fact]
    public void Rename_MissingId_ThrowsInvalidOperationException()
        => Should.Throw<InvalidOperationException>(() => _store.Rename(Guid.NewGuid(), "New Name"));
}
