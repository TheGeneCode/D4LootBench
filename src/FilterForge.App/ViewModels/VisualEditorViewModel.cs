using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThunderEagle.FilterForge.App.ViewModels.Conditions;
using ThunderEagle.FilterForge.Core.Data;
using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.App.ViewModels;

public partial class VisualEditorViewModel : ObservableObject
{
    private readonly IConditionViewModelFactory _conditionFactory;

    public ObservableCollection<FilterRuleViewModel> Rules { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    private FilterRuleViewModel? _selectedRule;

    [ObservableProperty]
    private string _filterName = null!;

    [ObservableProperty]
    private PlayerClass _selectedClass = PlayerClass.All;

    public static IReadOnlyList<PlayerClass> PlayerClasses { get; } = Enum.GetValues<PlayerClass>();

    public string RuleCountDisplay => $"{Rules.Count} / {FilterRuleset.MaxRuleCount}";

    public VisualEditorViewModel(IConditionViewModelFactory conditionFactory, FilterRuleset ruleset)
    {
        _conditionFactory = conditionFactory;
        _filterName       = ruleset.Name;
        foreach (var rule in ruleset.Rules)
            Rules.Add(MakeRuleVm(rule));
        Rules.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(RuleCountDisplay));
            OnPropertyChanged(nameof(IsAtRuleLimit));
            AddRuleCommand.NotifyCanExecuteChanged();
        };
    }

    public bool IsAtRuleLimit => Rules.Count >= FilterRuleset.MaxRuleCount;

    public FilterRuleset BuildRuleset() =>
        new(FilterName, Rules.Select(r => r.BuildRule()));

    [RelayCommand(CanExecute = nameof(CanAddRule))]
    private void AddRule()
    {
        var vm = MakeRuleVm(new FilterRule($"Rule #{Rules.Count + 1}", Visibility.Show, FilterColors.GameDefault, []));
        Rules.Add(vm);
        SelectedRule = vm;
    }

    private bool CanAddRule() => Rules.Count < FilterRuleset.MaxRuleCount;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteRule()
    {
        if (SelectedRule is null) return;
        var idx = Rules.IndexOf(SelectedRule);
        Rules.Remove(SelectedRule);
        SelectedRule = Rules.Count > 0 ? Rules[Math.Min(idx, Rules.Count - 1)] : null;
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedRule is null) return;
        var idx = Rules.IndexOf(SelectedRule);
        Rules.Move(idx, idx - 1);
        RefreshMoveCanExecute();
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedRule is null) return;
        var idx = Rules.IndexOf(SelectedRule);
        Rules.Move(idx, idx + 1);
        RefreshMoveCanExecute();
    }

    private bool HasSelection()  => SelectedRule is not null;
    private bool CanMoveUp()     => SelectedRule is not null && Rules.IndexOf(SelectedRule) > 0;
    private bool CanMoveDown()   => SelectedRule is not null && Rules.IndexOf(SelectedRule) < Rules.Count - 1;

    private void RefreshMoveCanExecute()
    {
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedClassChanged(PlayerClass value)
    {
        foreach (var rule in Rules)
            rule.ApplyClassFilter(value);
    }

    private FilterRuleViewModel MakeRuleVm(FilterRule rule)
    {
        var vm = new FilterRuleViewModel(_conditionFactory, rule,
            self => Rules.Where(r => r != self).Select(r => r.Color));
        vm.ApplyClassFilter(SelectedClass);
        return vm;
    }
}
