using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace D4Loot.App.ViewModels.Conditions;

public sealed record PickerEntry(uint Hash, string DisplayName);

public sealed partial class PickerViewModel : ObservableObject
{
    private IReadOnlyList<PickerEntry> _source;

    public ObservableCollection<PickerEntry> Selected { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredAvailable))]
    private Func<PickerEntry, bool>? _sourceFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredAvailable))]
    private string _searchText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddItemCommand))]
    private PickerEntry? _selectedAvailable;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveItemCommand))]
    private PickerEntry? _selectedCurrent;

    /// <summary>Optional game-enforced limit on total selected items.</summary>
    public int? MaxSelectionCount { get; init; }

    /// <summary>Optional external limit check (e.g., shared limit across pickers).</summary>
    public Func<bool>? ExternalAtMax { get; set; }

    public bool IsAtMax =>
        (MaxSelectionCount.HasValue && Selected.Count >= MaxSelectionCount.Value) ||
        (ExternalAtMax is not null && ExternalAtMax());

    public string SelectionCountDisplay =>
        MaxSelectionCount.HasValue
            ? $"{Selected.Count} / {MaxSelectionCount.Value}"
            : $"{Selected.Count}";

    public PickerViewModel(IEnumerable<PickerEntry> source)
    {
        _source = source.OrderBy(e => e.DisplayName).ToList();
        Selected.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(FilteredAvailable));
            OnPropertyChanged(nameof(IsAtMax));
            OnPropertyChanged(nameof(SelectionCountDisplay));
            AddItemCommand.NotifyCanExecuteChanged();
            ClearAllCommand.NotifyCanExecuteChanged();
        };
    }

    /// <summary>Replaces the available-item source, removing stale selections.</summary>
    public void ReplaceSource(IEnumerable<PickerEntry> newSource)
    {
        var newList = newSource.OrderBy(e => e.DisplayName).ToList();
        var validHashes = newList.Select(e => e.Hash).ToHashSet();

        for (int i = Selected.Count - 1; i >= 0; i--)
        {
            if (!validHashes.Contains(Selected[i].Hash))
                Selected.RemoveAt(i);
        }

        _source = newList;
        OnPropertyChanged(nameof(FilteredAvailable));
    }

    public IEnumerable<PickerEntry> FilteredAvailable
    {
        get
        {
            var selectedHashes = Selected.Select(e => e.Hash).ToHashSet();
            var q = _source.Where(e => !selectedHashes.Contains(e.Hash));
            if (SourceFilter is not null)
                q = q.Where(e => SourceFilter(e));
            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(e => e.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            return q;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void AddItem()
    {
        if (SelectedAvailable is not null)
        {
            Selected.Add(SelectedAvailable);
            SelectedAvailable = null;
        }
    }

    private bool CanAdd() => SelectedAvailable is not null && !IsAtMax;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void RemoveItem()
    {
        if (SelectedCurrent is not null)
        {
            Selected.Remove(SelectedCurrent);
            SelectedCurrent = null;
        }
    }

    private bool CanRemove() => SelectedCurrent is not null;

    [RelayCommand(CanExecute = nameof(CanClearAll))]
    private void ClearAll()
    {
        Selected.Clear();
        SelectedCurrent = null;
    }

    private bool CanClearAll() => Selected.Count > 0;
}
