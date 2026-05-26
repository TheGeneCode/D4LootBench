using CommunityToolkit.Mvvm.ComponentModel;
using D4LootBench.App.ViewModels;
using D4LootBench.Core.Models;

namespace D4LootBench.App.ViewModels.Conditions;

public sealed partial class CodexConditionViewModel : ConditionViewModel
{
    public override string TypeName => "Codex of Power";
    public override Condition BuildModel() => new CodexCondition();
}
