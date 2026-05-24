using System.Text.Json.Serialization;

namespace D4Loot.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ItemPowerCondition),     "itemPower")]
[JsonDerivedType(typeof(RarityCondition),        "rarity")]
[JsonDerivedType(typeof(ItemPropertiesCondition),"itemProperties")]
[JsonDerivedType(typeof(GreaterAffixCondition),  "greaterAffix")]
[JsonDerivedType(typeof(CodexCondition),         "codex")]
[JsonDerivedType(typeof(ItemTypeCondition),      "itemType")]
[JsonDerivedType(typeof(AffixCondition),         "affix")]
[JsonDerivedType(typeof(OptionalAffixCondition), "optionalAffix")]
[JsonDerivedType(typeof(SpecificUniqueCondition), "specificUnique")]
[JsonDerivedType(typeof(UnknownCondition),       "unknown")]
public abstract record Condition;

/// <summary>Type 0 — items within an item power range (inclusive).</summary>
public sealed record ItemPowerCondition(int Minimum, int Maximum) : Condition;

public sealed record RarityCondition(RarityFlags Mask) : Condition;

/// <summary>Type 2 — filters by item property (e.g. Ancestral). 1=None, 4=Ancestral.</summary>
public sealed record ItemPropertiesCondition(int PropertyMask) : Condition;

/// <summary>Type 3 — items with at least <see cref="MinimumCount"/> Greater Affixes.</summary>
public sealed record GreaterAffixCondition(int MinimumCount) : Condition;

/// <summary>Type 4 — items usable to upgrade a Codex of Power entry.</summary>
public sealed record CodexCondition : Condition;

/// <summary>Type 5 — item type hash IDs (e.g. Charm, Seal).</summary>
public sealed record ItemTypeCondition(IReadOnlyList<uint> TypeIds) : Condition;

/// <summary>Type 6 — affix hash IDs with a minimum-present threshold (AND/count semantics).</summary>
/// <remarks>Field 3 in the wire format encodes greater-affix pairs: each sub-message
/// stores an affix ID plus a companion value (meaning TBD).</remarks>
public sealed record GreaterAffixEntry(uint AffixId, uint Value);

public sealed record AffixCondition(IReadOnlyList<uint> AffixIds, int MinimumCount) : Condition
{
    public IReadOnlyList<GreaterAffixEntry> GreaterEntries { get; init; } = [];
    public int Field5 { get; init; }
}

/// <summary>Type 7 — affix hash IDs with OR semantics: matches if the item has any of the listed affixes.</summary>
public sealed record OptionalAffixCondition(IReadOnlyList<uint> AffixIds) : Condition;

/// <summary>Type 8 — matches specific named Unique items by sno ID.</summary>
public sealed record SpecificUniqueCondition(IReadOnlyList<uint> UniqueIds) : Condition;

/// <summary>Preserves raw bytes for condition types not yet mapped, enabling lossless round-trips.</summary>
public sealed record UnknownCondition(int ConditionType, byte[] RawBytes) : Condition;
