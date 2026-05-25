using CommunityToolkit.Mvvm.ComponentModel;
using ThunderEagle.FilterForge.App.ViewModels;
using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.App.ViewModels.Conditions;

public sealed partial class CodexConditionViewModel : ConditionViewModel
{
    public override string TypeName => "Codex of Power";
    public override Condition BuildModel() => new CodexCondition();
}
