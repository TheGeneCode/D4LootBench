using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace D4LootBench.App.Views.Help;

public partial class FilterRulesTopic : UserControl
{
    public FilterRulesTopic() => InitializeComponent();

    private void OnHyperlinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
