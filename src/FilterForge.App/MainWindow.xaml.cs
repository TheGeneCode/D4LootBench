using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ThunderEagle.FilterForge.App.ViewModels;
using ThunderEagle.FilterForge.App.Views;

namespace ThunderEagle.FilterForge.App;

public partial class MainWindow
{
    private readonly MainWindowViewModel _vm;
    private double _savedPanelHeight = 220;
    private HelpWindow? _helpWindow;

    public MainWindow(MainWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        _vm.ShowRawEditorRequested += OnShowRawEditorRequested;
        _vm.OpenHelpRequested     += OnOpenHelpRequested;
        _vm.ShowAboutRequested    += OnShowAboutRequested;
        _vm.PropertyChanged       += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsAiPanelVisible))
            ApplyAiPanelLayout(_vm.IsAiPanelVisible);
    }

    private void ApplyAiPanelLayout(bool visible)
    {
        var splitterRow = ContentGrid.RowDefinitions[1];
        var panelRow    = ContentGrid.RowDefinitions[2];

        if (visible)
        {
            splitterRow.Height    = new GridLength(4);
            panelRow.MinHeight    = 140;
            panelRow.Height       = new GridLength(_savedPanelHeight);
            AiSplitter.Visibility = Visibility.Visible;
            AiPanel.Visibility    = Visibility.Visible;
        }
        else
        {
            // Preserve whatever height the user last dragged to
            if (panelRow.ActualHeight > 0)
                _savedPanelHeight = panelRow.ActualHeight;

            splitterRow.Height    = new GridLength(0);
            panelRow.MinHeight    = 0;
            panelRow.Height       = new GridLength(0);
            AiSplitter.Visibility = Visibility.Collapsed;
            AiPanel.Visibility    = Visibility.Collapsed;
        }
    }

    private void OnShowRawEditorRequested(RawEditorViewModel vm)
    {
        var window = new RawEditorWindow
        {
            DataContext = vm,
            Owner = this
        };
        window.Show();
    }

    private void OnOpenHelpRequested(string topic)
    {
        if (_helpWindow is null || !_helpWindow.IsLoaded)
        {
            _helpWindow = new HelpWindow { Owner = this };
            _helpWindow.Closed += (_, _) => _helpWindow = null;
            _helpWindow.Show();
        }
        else
        {
            _helpWindow.Activate();
        }
        _helpWindow.NavigateTo(topic);
    }

    private void OnShowAboutRequested()
    {
        new AboutDialog { Owner = this }.ShowDialog();
    }
}
