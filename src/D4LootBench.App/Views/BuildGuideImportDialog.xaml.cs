using System.Windows;
using D4LootBench.App.ViewModels;
using D4LootBench.Ai.Import;
using D4LootBench.Core.Import;

namespace D4LootBench.App.Views;

public partial class BuildGuideImportDialog : Window
{
    public BuildGuideImportViewModel Vm { get; }

    public BuildGuideImportDialog(BuildGuideImporter importer, BuildGuideFilterGenerator generator)
    {
        InitializeComponent();
        Vm = new BuildGuideImportViewModel(importer, generator);
        DataContext = Vm;
        Vm.ImportSucceeded += () => DialogResult = true;
    }
}
