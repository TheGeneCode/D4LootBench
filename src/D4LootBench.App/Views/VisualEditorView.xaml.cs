using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using D4LootBench.App.Services;

namespace D4LootBench.App.Views;

public partial class VisualEditorView : UserControl
{
    private WindowSettingsService? _windowSettings;

    public VisualEditorView()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _windowSettings = App.Services.GetRequiredService<WindowSettingsService>();
        EditorGrid.ColumnDefinitions[0].Width = new GridLength(_windowSettings.RuleListWidth);
        RuleListSplitter.DragCompleted += OnSplitterDragCompleted;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        RuleListSplitter.DragCompleted -= OnSplitterDragCompleted;
    }

    private void OnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_windowSettings is not null)
            _windowSettings.RuleListWidth = EditorGrid.ColumnDefinitions[0].ActualWidth;
    }
}
