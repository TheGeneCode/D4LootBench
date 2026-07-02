using D4LootBench.Core.Progression;

namespace D4LootBench.App.ViewModels.Progression;

/// <summary>A selectable "meets goal" strictness preset.</summary>
/// <param name="Threshold">The underlying threshold.</param>
/// <param name="Label">The display label.</param>
public sealed record ThresholdOption(MeetsGoalThreshold Threshold, string Label);
