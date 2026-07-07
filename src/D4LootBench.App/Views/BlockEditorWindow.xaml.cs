using System.Windows;

namespace D4LootBench.App.Views;

/// <summary>Modal editor for a single static rule block, hosting the shared <see cref="VisualEditorView"/>.
/// The <see cref="Window.DataContext"/> is a <c>VisualEditorViewModel</c> seeded from the block's rules;
/// on OK the caller reads that VM's <c>BuildRuleset()</c>.</summary>
public partial class BlockEditorWindow : Window
{
    /// <summary>Initializes a new instance of the <see cref="BlockEditorWindow"/> class.</summary>
    public BlockEditorWindow() => InitializeComponent();

    private void Ok(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
