using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace ThunderEagle.FilterForge.App.Views.Help;

public partial class AttributionTopic : UserControl
{
    public AttributionTopic() => InitializeComponent();

    private void OnHyperlinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
