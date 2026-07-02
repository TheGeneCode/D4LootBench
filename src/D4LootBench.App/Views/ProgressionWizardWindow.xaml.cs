using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using D4LootBench.App.ViewModels.Progression;
using D4LootBench.Core.Models;
using Microsoft.Win32;

namespace D4LootBench.App.Views;

/// <summary>The four-step progression-filter wizard window. Code-behind owns only the two WPF-specific
/// concerns — turning a clipboard/file image into a PNG stream for the VM, and forwarding the VM's
/// "open in editor" request to <see cref="Window.DialogResult"/>. All other behaviour binds to the VM.</summary>
public partial class ProgressionWizardWindow : Window
{
    /// <summary>Initializes a new instance of the <see cref="ProgressionWizardWindow"/> class.</summary>
    /// <param name="vm">The wizard view model.</param>
    public ProgressionWizardWindow(ProgressionWizardViewModel vm)
    {
        InitializeComponent();
        Vm = vm;
        DataContext = vm;
        vm.OpenInEditorRequested += rs =>
        {
            RulesetForEditor = rs;
            DialogResult = true;
        };
    }

    /// <summary>Gets the wizard view model.</summary>
    public ProgressionWizardViewModel Vm { get; }

    /// <summary>Gets the ruleset the user chose to open in the editor, or <c>null</c> if they only copied the code.</summary>
    public FilterRuleset? RulesetForEditor { get; private set; }

    private static MemoryStream ToPngStream(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        var ms = new MemoryStream();
        encoder.Save(ms);
        ms.Position = 0;
        return ms;
    }

    private async void PasteScreenshot(object sender, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsImage())
        {
            Vm.StatusText = "No image on the clipboard.";
            Vm.HasError = true;
            return;
        }

        var bmp = Clipboard.GetImage();
        if (bmp is null)
        {
            return;
        }

        await using var stream = ToPngStream(bmp);
        await Vm.AddGearFromImageAsync(stream);
    }

    private async void OpenImageFile(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|All Files|*.*" };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        await using var stream = File.OpenRead(dlg.FileName);
        await Vm.AddGearFromImageAsync(stream);
    }
}
