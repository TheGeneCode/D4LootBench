using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;

namespace D4LootBench.App.ViewModels.Progression;

/// <summary>Observable wrapper over a mutable <see cref="GearReviewSession.ItemDraft"/>; slot/power/rarity
/// edits write straight through to the draft so <see cref="GearReviewSession.Build"/> reflects them.</summary>
/// <param name="draft">The item draft to wrap.</param>
public sealed partial class GearItemDraftViewModel(GearReviewSession.ItemDraft draft) : ObservableObject
{
    /// <summary>Gets the selectable gear slots for combo binding.</summary>
    public static IReadOnlyList<GearSlot> AvailableSlots { get; } = Enum.GetValues<GearSlot>();

    /// <summary>Gets the selectable rarities for combo binding.</summary>
    public static IReadOnlyList<ItemRarity> AvailableRarities { get; } = Enum.GetValues<ItemRarity>();

    /// <summary>Gets the selectable item-type names for combo binding.</summary>
    public static IReadOnlyList<string> AvailableItemTypes { get; } = ItemTypeDatabase.All.Select(t => t.Name).ToList();

    /// <summary>Gets or sets the corrected item-type display name, or <c>null</c> when unmatched. Weapon
    /// slot identity depends on it, so review can correct an OCR misread.</summary>
    public string? ItemTypeName
    {
        get => draft.ItemTypeName;
        set
        {
            if (draft.ItemTypeName == value)
            {
                return;
            }

            draft.ItemTypeName = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Gets a value indicating whether the item needs review (low parse confidence).</summary>
    public bool NeedsReview => draft.NeedsReview;

    /// <summary>Gets the parse warnings carried through for display.</summary>
    public IReadOnlyList<string> Warnings => draft.Warnings;

    /// <summary>Gets the affix draft wrappers.</summary>
    public ObservableCollection<GearAffixDraftViewModel> Affixes { get; } =
        new(draft.Affixes.Select(a => new GearAffixDraftViewModel(a)));

    /// <summary>Gets or sets the corrected slot.</summary>
    public GearSlot Slot
    {
        get => draft.Slot;
        set
        {
            if (draft.Slot == value)
            {
                return;
            }

            draft.Slot = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Gets or sets the corrected item power.</summary>
    public int? ItemPower
    {
        get => draft.ItemPower;
        set
        {
            if (draft.ItemPower == value)
            {
                return;
            }

            draft.ItemPower = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Gets or sets the corrected rarity.</summary>
    public ItemRarity Rarity
    {
        get => draft.Rarity;
        set
        {
            if (draft.Rarity == value)
            {
                return;
            }

            draft.Rarity = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Gets or sets a value indicating whether the item is ancestral.</summary>
    public bool IsAncestral
    {
        get => draft.IsAncestral;
        set
        {
            if (draft.IsAncestral == value)
            {
                return;
            }

            draft.IsAncestral = value;
            OnPropertyChanged();
        }
    }
}
