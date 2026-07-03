using D4LootBench.Core.Data;
using Shouldly;

namespace D4LootBench.Core.Tests.Data;

/// <summary>
/// Boundary coverage for <see cref="NameResolver"/> — the shared name→hash resolver used by OCR
/// parsing, guide import, the LLM assistant, and progression filter generation. Focuses on the
/// prefix-insensitive exact tier added between exact and fuzzy matching in <c>TryResolveAffix</c>,
/// and its interaction with catalog affixes that share a normalized key. The colliding display name
/// exercised below is "All Damage Multiplier", which the catalog carries as seven distinct per-source
/// entries sharing the same display name.
/// </summary>
public sealed class NameResolverTests
{
    private static NameResolver NewResolver() => new(new FilterDataService());

    // ── Exact tier ───────────────────────────────────────────────────────────

    [Fact]
    public void TryResolveAffix_ExactCatalogNameNoPrefix_ResolvesViaExactTier()
    {
        // "Maximum Life" carries no sign prefix in the catalog — an exact dictionary hit.
        var resolver = NewResolver();

        var resolved = resolver.TryResolveAffix("Maximum Life", out var hash, out var suggestions);

        resolved.ShouldBeTrue();
        hash.ShouldNotBe(0u);
        suggestions.ShouldBeEmpty();
    }

    // ── Prefix-insensitive exact tier (the new fix) ─────────────────────────

    [Fact]
    public void TryResolveAffix_SignlessPhraseForPrefixedCatalogEntry_ResolvesViaNormalizedTier()
    {
        // Catalog only has "+Movement Speed" (no bare "Movement Speed" entry). A guide/OCR phrase
        // never carries the sign, so exact match fails and the normalized-key tier must catch it —
        // even though the fuzzy pass alone would be ambiguous against "Evade Grants Movement Speed".
        var resolver = NewResolver();

        var resolved = resolver.TryResolveAffix("Movement Speed", out var hash, out _);

        resolved.ShouldBeTrue();
        resolver.TryResolveAffix("+Movement Speed", out var expectedHash, out _);
        hash.ShouldBe(expectedHash);
    }

    [Fact]
    public void TryResolveAffix_SkillRankPhrase_ResolvesToSkillRankAffix()
    {
        // Guides/game render skill-rank affixes as "Ranks to <Skill>" ("+X Ranks to Whirlwind"), but
        // the catalog names them "+<Skill>" (e.g. "+Whirlwind"). The rank phrasing must resolve to the
        // same skill-rank affix instead of being dropped.
        var resolver = NewResolver();

        var resolved = resolver.TryResolveAffix("Ranks to Whirlwind", out var hash, out _);

        resolved.ShouldBeTrue();
        resolver.TryResolveAffix("+Whirlwind", out var expectedHash, out _);
        hash.ShouldBe(expectedHash);
    }

    [Fact]
    public void IsKnownAffixPhrase_SkillRankPhrase_RecognizedAsAffix()
    {
        // The Maxroll parser uses this to tell a first-line affix from an item name. A skill-rank
        // phrase must read as an affix so it is not swallowed as the slot's item name.
        var resolver = NewResolver();

        resolver.IsKnownAffixPhrase("Ranks to Whirlwind").ShouldBeTrue();
    }

    [Fact]
    public void TryResolveAffix_LowercaseSignlessPhrase_ResolvesViaNormalizedTier_CaseInsensitive()
    {
        // NormalizeAffixKey lowercases before comparing, independent of the sign-stripping behavior.
        var resolver = NewResolver();

        var resolved = resolver.TryResolveAffix("movement speed", out var hash, out _);

        resolved.ShouldBeTrue();
        resolver.TryResolveAffix("+Movement Speed", out var expectedHash, out _);
        hash.ShouldBe(expectedHash);
    }

    [Fact]
    public void TryResolveAffix_NormalizedTierDoesNotConflateLongerContainingAffix()
    {
        // Regression guard: "Movement Speed" must resolve to the "+Movement Speed" affix itself, not
        // to "Evade Grants Movement Speed" (which merely contains the phrase as a substring — this is
        // exactly the ambiguity the fuzzy-only pass used to choke on before the normalized tier existed).
        var resolver = NewResolver();

        resolver.TryResolveAffix("Movement Speed", out var hash, out _);
        resolver.TryResolveAffix("Evade Grants Movement Speed", out var evadeHash, out _);

        hash.ShouldNotBe(evadeHash);
    }

    // ── Ambiguous normalized key must fall through to fuzzy, not mis-resolve or throw ──

    [Fact]
    public void TryResolveAffix_AmbiguousNormalizedKey_FallsThroughToFuzzy_StaysUnresolved()
    {
        // The catalog carries "All Damage Multiplier" as SEVEN distinct entries (different hashes,
        // identical display name — one per class-agnostic source). A lowercase, signless query
        // normalizes to the same key for all seven, so the normalized tier must NOT pick one
        // arbitrarily (keyed.Count == 7) — it must fall through to fuzzy, where all seven also match
        // via substring containment, so it correctly stays ambiguous rather than resolving to an
        // arbitrary hash or throwing (e.g. a Single()-style crash on the multi-match set).
        var resolver = NewResolver();

        var ex = Record.Exception(() => resolver.TryResolveAffix("all damage multiplier", out _, out _));
        ex.ShouldBeNull();

        var resolved = resolver.TryResolveAffix("all damage multiplier", out var hash, out var suggestions);

        resolved.ShouldBeFalse();
        hash.ShouldBe(0u);
        suggestions.ShouldNotBeEmpty();
    }

    [Fact]
    public void TryResolveAffix_ExactCaseMatchOnCollidingName_BypassesAmbiguity()
    {
        // Exact-case "All Damage Multiplier" is an unambiguous dictionary hit on the exact tier
        // (last-write-wins in AffixDatabase.ByName) — it must resolve directly without ever reaching
        // the ambiguous normalized/fuzzy tiers, even though seven entries share this display name.
        var resolver = NewResolver();

        var resolved = resolver.TryResolveAffix("All Damage Multiplier", out var hash, out var suggestions);

        resolved.ShouldBeTrue();
        hash.ShouldNotBe(0u);
        suggestions.ShouldBeEmpty();
    }

    [Fact]
    public void TryResolveAffix_SignPrefixedVariantOfCollidingName_FallsThroughToFuzzy_StaysUnresolved()
    {
        // "+All Damage Multiplier" does not exact-match any catalog entry (none carry a sign
        // prefix), so it reaches the normalized tier, which is ambiguous across all seven hashes,
        // then fuzzy, which is equally ambiguous — must stay unresolved, not silently pick one.
        var resolver = NewResolver();

        var resolved = resolver.TryResolveAffix("+All Damage Multiplier", out var hash, out var suggestions);

        resolved.ShouldBeFalse();
        hash.ShouldBe(0u);
        suggestions.ShouldNotBeEmpty();
    }

    // ── Unresolved / degenerate inputs ───────────────────────────────────────

    [Fact]
    public void TryResolveAffix_UnknownPhrase_ReturnsFalseWithSuggestions()
    {
        var resolver = NewResolver();

        var resolved = resolver.TryResolveAffix("Zzznonexistent Affix Phrase", out var hash, out _);

        resolved.ShouldBeFalse();
        hash.ShouldBe(0u);
    }

    [Fact]
    public void TryResolveAffix_EmptyString_ReturnsFalseNoCrash()
    {
        var resolver = NewResolver();

        var ex = Record.Exception(() => resolver.TryResolveAffix(string.Empty, out _, out _));

        ex.ShouldBeNull();
        resolver.TryResolveAffix(string.Empty, out var hash, out _).ShouldBeFalse();
        hash.ShouldBe(0u);
    }

    [Fact]
    public void TryResolveAffix_LeadingDigitNoiseNotStrippedByNormalizedTier_StillResolvesViaFuzzySubstring()
    {
        // NormalizeAffixKey only strips a leading run of '+'/'%' — NOT digits — so a phrase like
        // "5 Maximum Life" (stray OCR digit) fails the normalized-key tier (key "5 maximum life" !=
        // "maximum life"). It still resolves because the separate fuzzy pass does plain substring
        // Contains() in both directions, and "5 maximum life" contains "maximum life" unambiguously.
        var resolver = NewResolver();

        var resolved = resolver.TryResolveAffix("5 Maximum Life", out var hash, out _);

        resolved.ShouldBeTrue();
        resolver.TryResolveAffix("Maximum Life", out var expectedHash, out _);
        hash.ShouldBe(expectedHash);
    }

    // ── IsKnownAffixPhrase: used for parser classification, distinct semantics from TryResolveAffix ──

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsKnownAffixPhrase_NullOrWhitespace_ReturnsFalse(string? phrase)
    {
        var resolver = NewResolver();

        resolver.IsKnownAffixPhrase(phrase!).ShouldBeFalse();
    }

    [Fact]
    public void IsKnownAffixPhrase_ExactCatalogMatch_ReturnsTrue()
    {
        var resolver = NewResolver();

        resolver.IsKnownAffixPhrase("Maximum Life").ShouldBeTrue();
    }

    [Fact]
    public void IsKnownAffixPhrase_AmbiguousSharedName_StillReturnsTrue()
    {
        // Unlike TryResolveAffix (which requires an unambiguous single match to auto-resolve),
        // IsKnownAffixPhrase only asks "does this phrase NAME an affix" via the normalized-key identity
        // check — so "all damage multiplier" (seven colliding hashes) that leaves TryResolveAffix
        // unresolved must still classify as an affix phrase here. MaxrollParser.LooksLikeAffix depends
        // on this so an ambiguous first-line affix is kept rather than swallowed as an item name.
        var resolver = NewResolver();

        resolver.TryResolveAffix("all damage multiplier", out _, out _).ShouldBeFalse(); // sanity: still ambiguous
        resolver.IsKnownAffixPhrase("all damage multiplier").ShouldBeTrue();
    }

    [Fact]
    public void IsKnownAffixPhrase_LeadingPortionOfCatalogName_ReturnsTrue()
    {
        // A guide phrase that is a leading portion of a longer catalog name ("Vulnerable Damage" for
        // "Vulnerable Damage Multiplier") must classify as an affix — the catalog name contains the
        // query. This is the direction item-name substrings ("...Strength") do NOT satisfy.
        var resolver = NewResolver();

        resolver.IsKnownAffixPhrase("Vulnerable Damage").ShouldBeTrue();
    }

    [Fact]
    public void IsKnownAffixPhrase_UnknownGarbage_ReturnsFalse()
    {
        var resolver = NewResolver();

        resolver.IsKnownAffixPhrase("Zzznonexistent Garbage Phrase Xyz").ShouldBeFalse();
    }

    [Fact]
    public void IsKnownAffixPhrase_ItemNameContainingAffixSubstring_ReturnsFalse()
    {
        // A real item/aspect name that merely contains a catalog affix word ("Strength" inside
        // "Girdle of Boundless Strength") must NOT be classified as an affix — the whole-phrase
        // identity check rejects it, so the MaxrollParser keeps it as the item name.
        var resolver = NewResolver();

        resolver.IsKnownAffixPhrase("Girdle of Boundless Strength").ShouldBeFalse();
    }
}
