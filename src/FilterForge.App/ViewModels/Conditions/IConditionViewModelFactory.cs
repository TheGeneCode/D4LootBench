using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.App.ViewModels.Conditions;

/// <summary>
/// Maps between <see cref="Condition"/> domain models and their <see cref="ConditionViewModel"/>
/// counterparts. Centralizing dispatch keeps <see cref="FilterRuleViewModel"/> free of
/// per-type switches and gives a single place to update when a new condition type is added.
/// </summary>
public interface IConditionViewModelFactory
{
    /// <summary>Wraps an existing domain <see cref="Condition"/> in its matching ViewModel.</summary>
    ConditionViewModel FromModel(Condition c);

    /// <summary>Creates an empty ViewModel for the given <see cref="ConditionType"/>.</summary>
    ConditionViewModel CreateNew(ConditionType type);

    /// <summary>Returns the <see cref="ConditionType"/> that the given ViewModel represents.</summary>
    ConditionType? GetConditionType(ConditionViewModel vm);
}
