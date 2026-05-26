using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ThunderEagle.FilterForge.App.ViewModels.Conditions;

namespace ThunderEagle.FilterForge.App.Views;

public partial class ItemPickerControl
{
    public ItemPickerControl() => InitializeComponent();

    private void AvailableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is PickerViewModel vm)
            vm.SyncAvailableSelection(AvailableList.SelectedItems);
    }

    private void SelectedList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is PickerViewModel vm)
            vm.SyncCurrentSelection(SelectedList.SelectedItems);
    }

    private void AddButton_Click(object _, RoutedEventArgs _1)
    {
        if (DataContext is not PickerViewModel vm) return;
        var items = AvailableList.SelectedItems.OfType<PickerEntry>().ToList();
        if (items.Count > 0) vm.AddItems(items);
    }

    private void RemoveButton_Click(object _, RoutedEventArgs _1)
    {
        if (DataContext is not PickerViewModel vm) return;
        var items = SelectedList.SelectedItems.OfType<PickerEntry>().ToList();
        if (items.Count > 0) vm.RemoveItems(items);
    }

    private void AvailableList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PickerViewModel vm) return;
        if (e.OriginalSource is FrameworkElement { DataContext: PickerEntry item })
            vm.AddItems([item]);
    }

    private void SelectedList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PickerViewModel vm) return;
        if (e.OriginalSource is FrameworkElement { DataContext: PickerEntry item })
            vm.RemoveItems([item]);
    }
}
