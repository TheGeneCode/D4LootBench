using CommunityToolkit.Mvvm.ComponentModel;
using D4LootBench.Core.Gear;

namespace D4LootBench.App.ViewModels.Progression;

/// <summary>Observable wrapper over a mutable <see cref="GearReviewSession.AffixDraft"/>; writes straight
/// through to the draft so <see cref="GearReviewSession.Build"/> reflects user edits.</summary>
/// <param name="draft">The affix draft to wrap.</param>
public sealed partial class GearAffixDraftViewModel(GearReviewSession.AffixDraft draft) : ObservableObject
{
    /// <summary>Gets the original OCR text.</summary>
    public string RawText => draft.RawText;

    /// <summary>Gets the resolved catalog name, or <c>null</c> when unresolved.</summary>
    public string? ResolvedName => draft.ResolvedName;

    /// <summary>Gets or sets a value indicating whether this is a greater affix.</summary>
    public bool IsGreaterAffix
    {
        get => draft.IsGreaterAffix;
        set
        {
            if (draft.IsGreaterAffix == value)
            {
                return;
            }

            draft.IsGreaterAffix = value;
            OnPropertyChanged();
        }
    }
}
