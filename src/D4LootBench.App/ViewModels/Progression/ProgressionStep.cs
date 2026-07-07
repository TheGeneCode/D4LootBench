namespace D4LootBench.App.ViewModels.Progression;

/// <summary>Active panel in the progression wizard.</summary>
public enum ProgressionStep
{
    /// <summary>Pick, manage, or start a saved profile.</summary>
    Profiles,

    /// <summary>Read gear tooltips from screenshots.</summary>
    ReadGear,

    /// <summary>Review and correct the read gear.</summary>
    Review,

    /// <summary>Paste the goal build guide and pick a threshold.</summary>
    Goal,

    /// <summary>Show the generated share code.</summary>
    Result,
}
