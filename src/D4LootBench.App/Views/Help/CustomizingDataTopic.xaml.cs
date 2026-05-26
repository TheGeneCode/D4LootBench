using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace D4LootBench.App.Views.Help;

public partial class CustomizingDataTopic : UserControl
{
    private readonly Func<Task> _extractAction;

    public CustomizingDataTopic(Func<Task> extractAction)
    {
        _extractAction = extractAction;
        InitializeComponent();
    }

    private async void OnExtractClick(object sender, RoutedEventArgs e)
    {
        ExtractButton.IsEnabled = false;
        try
        {
            await _extractAction();
        }
        finally
        {
            ExtractButton.IsEnabled = true;
        }
    }

    private void OnHyperlinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
