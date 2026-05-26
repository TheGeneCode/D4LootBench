using D4LootBench.Core.Models;

namespace D4LootBench.App.ViewModels.Conditions;

public sealed partial class UnknownConditionViewModel : ConditionViewModel
{
    private readonly UnknownCondition _model;

    public UnknownConditionViewModel(UnknownCondition model) => _model = model;

    public override string TypeName => $"Unknown ({_model.ConditionType})";
    public override string Summary  => $"{_model.RawBytes.Length} raw byte(s)";
    public override Condition BuildModel() => _model;
}
