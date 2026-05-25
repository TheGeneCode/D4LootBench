using ThunderEagle.FilterForge.App.ViewModels;
using ThunderEagle.FilterForge.App.Views;

namespace ThunderEagle.FilterForge.App;

public partial class MainWindow
{
    private readonly MainWindowViewModel _vm;

    public MainWindow(MainWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        _vm.ShowRawEditorRequested += OnShowRawEditorRequested;
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
}
