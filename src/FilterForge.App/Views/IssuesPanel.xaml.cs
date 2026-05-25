using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace ThunderEagle.FilterForge.App.Views;

public partial class IssuesPanel : UserControl
{
    public static readonly DependencyProperty IssuesProperty =
        DependencyProperty.Register(
            nameof(Issues),
            typeof(IEnumerable),
            typeof(IssuesPanel),
            new PropertyMetadata(null));

    public IEnumerable? Issues
    {
        get => (IEnumerable?)GetValue(IssuesProperty);
        set => SetValue(IssuesProperty, value);
    }

    public IssuesPanel()
    {
        InitializeComponent();
    }
}
