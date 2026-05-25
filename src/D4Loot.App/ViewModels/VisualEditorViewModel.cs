using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D4Loot.Core.Data;
using D4Loot.Core.Models;

namespace D4Loot.App.ViewModels;

public partial class VisualEditorViewModel : ObservableObject
{
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

    public VisualEditorViewModel(FilterRuleset ruleset)
    {
        _filterName = ruleset.Name;
        foreach (var rule in ruleset.Rules)
            Rules.Add(MakeRuleVm(rule));
        Rules.CollectionChanged += (_, _) => OnPropertyChanged(nameof(RuleCountDisplay));
    }

    public FilterRuleset BuildRuleset() =>
        new(FilterName, Rules.Select(r => r.BuildRule()));

    [RelayCommand]
    private void AddRule()
    {
        var vm = MakeRuleVm(new FilterRule("New Rule", Visibility.Show, FilterColors.GameDefault, []));
        Rules.Add(vm);
        SelectedRule = vm;
    }

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
        var vm = new FilterRuleViewModel(rule, self => Rules.Where(r => r != self).Select(r => r.Color));
        vm.ApplyClassFilter(SelectedClass);
        return vm;
    }
}
