namespace D4LootBench.Core.Gear;

/// <summary>
/// Headless review state for one or more parsed gear items. Lets a human confirm/correct fields and
/// toggle greater-affix flags before the gear is consumed by later phases. A WPF observable wrapper
/// is deferred with the rest of the UI.
/// </summary>
public sealed class GearReviewSession
{
    private readonly List<ItemDraft> _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="GearReviewSession"/> class, seeding one draft per
    /// parsed item; <see cref="ItemDraft.NeedsReview"/> mirrors low confidence.
    /// </summary>
    /// <param name="parsed">The parse results to review.</param>
    public GearReviewSession(IEnumerable<GearParseResult> parsed)
    {
        _items = parsed.Select(ToDraft).ToList();
    }

    /// <summary>Gets the editable drafts, one per parsed item.</summary>
    public IReadOnlyList<ItemDraft> Items => _items;

    /// <summary>Flip the greater-affix flag on a single affix.</summary>
    /// <param name="itemIndex">Zero-based item index.</param>
    /// <param name="affixIndex">Zero-based affix index within the item.</param>
    public void ToggleGreaterAffix(int itemIndex, int affixIndex)
    {
        var affix = AffixAt(itemIndex, affixIndex);
        affix.IsGreaterAffix = !affix.IsGreaterAffix;
    }

    /// <summary>Manually correct an item's slot.</summary>
    /// <param name="itemIndex">Zero-based item index.</param>
    /// <param name="slot">The corrected slot.</param>
    public void SetSlot(int itemIndex, GearSlot slot)
        => ItemAt(itemIndex).Slot = slot;

    /// <summary>Materialize the corrected drafts back into immutable <see cref="GearItem"/>s.</summary>
    /// <returns>The confirmed, corrected gear items.</returns>
    public IReadOnlyList<GearItem> Build()
    {
        return _items.Select(draft => new GearItem
        {
            Slot = draft.Slot,
            ItemTypeName = draft.ItemTypeName,
            ItemPower = draft.ItemPower,
            Rarity = draft.Rarity,
            IsAncestral = draft.IsAncestral,
            UniqueHash = draft.UniqueHash,
            Affixes = draft.Affixes.Select(a => new GearAffix
            {
                RawText = a.RawText,
                ResolvedName = a.ResolvedName,
                AffixHash = a.AffixHash,
                IsGreaterAffix = a.IsGreaterAffix,
            }).ToList(),
        }).ToList();
    }

    private static ItemDraft ToDraft(GearParseResult result)
    {
        var item = result.Item;
        return new ItemDraft
        {
            Source = item,
            Slot = item.Slot,
            ItemTypeName = item.ItemTypeName,
            ItemPower = item.ItemPower,
            Rarity = item.Rarity,
            IsAncestral = item.IsAncestral,
            UniqueHash = item.UniqueHash,
            NeedsReview = result.Confidence == GearParseConfidence.Low,
            Warnings = result.Warnings,
            Affixes = item.Affixes.Select(a => new AffixDraft
            {
                RawText = a.RawText,
                ResolvedName = a.ResolvedName,
                AffixHash = a.AffixHash,
                IsGreaterAffix = a.IsGreaterAffix,
            }).ToList(),
        };
    }

    private ItemDraft ItemAt(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(itemIndex));
        }

        return _items[itemIndex];
    }

    private AffixDraft AffixAt(int itemIndex, int affixIndex)
    {
        var item = ItemAt(itemIndex);
        if (affixIndex < 0 || affixIndex >= item.Affixes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(affixIndex));
        }

        return item.Affixes[affixIndex];
    }

    /// <summary>Mutable per-item review draft.</summary>
    public sealed class ItemDraft
    {
        /// <summary>Gets the original parsed item (immutable reference data such as item-type name).</summary>
        public required GearItem Source { get; init; }

        /// <summary>Gets or sets corrected slot.</summary>
        public GearSlot Slot { get; set; }

        /// <summary>Gets or sets the corrected item-type name (settable so review can correct an OCR
        /// item-type misread — weapon slot identity depends on it).</summary>
        public string? ItemTypeName { get; set; }

        /// <summary>Gets or sets corrected item power.</summary>
        public int? ItemPower { get; set; }

        /// <summary>Gets or sets corrected rarity.</summary>
        public ItemRarity Rarity { get; set; }

        /// <summary>Gets or sets a value indicating whether the item is ancestral.</summary>
        public bool IsAncestral { get; set; }

        /// <summary>Gets or sets the resolved unique-item hash (settable so review can correct it).</summary>
        public uint? UniqueHash { get; set; }

        /// <summary>Gets editable affix drafts.</summary>
        public List<AffixDraft> Affixes { get; init; } = [];

        /// <summary>Gets a value indicating whether the item needs review (seeded from <see cref="GearParseConfidence.Low"/>).</summary>
        public bool NeedsReview { get; init; }

        /// <summary>Gets parse warnings carried through for display.</summary>
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    /// <summary>Mutable per-affix review draft.</summary>
    public sealed class AffixDraft
    {
        /// <summary>Gets original OCR line.</summary>
        public required string RawText { get; init; }

        /// <summary>Gets or sets resolved catalog name, editable during review.</summary>
        public string? ResolvedName { get; set; }

        /// <summary>Gets or sets resolved affix hash, editable during review.</summary>
        public uint? AffixHash { get; set; }

        /// <summary>Gets or sets a value indicating whether this is a greater affix (user-toggled).</summary>
        public bool IsGreaterAffix { get; set; }
    }
}
