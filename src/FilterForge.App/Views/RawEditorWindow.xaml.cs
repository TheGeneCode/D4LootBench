using System.ComponentModel;
using System.Windows;
using ThunderEagle.FilterForge.App.ViewModels;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Search;

namespace ThunderEagle.FilterForge.App.Views;

public partial class RawEditorWindow : Window
{
    private bool _editorChanging;
    private readonly FoldingManager _foldingManager;

    public RawEditorWindow()
    {
        InitializeComponent();

        SearchPanel.Install(Editor);
        _foldingManager = FoldingManager.Install(Editor.TextArea);

        DataContextChanged += OnDataContextChanged;
        Editor.TextChanged += OnEditorTextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RawEditorViewModel old)
            old.PropertyChanged -= OnViewModelPropertyChanged;

        if (e.NewValue is RawEditorViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            // Populate editor with initial JSON
            _editorChanging = true;
            Editor.Text = vm.JsonText;
            JsonFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
            _editorChanging = false;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RawEditorViewModel.JsonText) || _editorChanging) return;

        _editorChanging = true;
        Editor.Text = ((RawEditorViewModel)sender!).JsonText;
        JsonFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
        _editorChanging = false;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_editorChanging) return;
        _editorChanging = true;

        if (DataContext is RawEditorViewModel vm)
            vm.JsonText = Editor.Text;

        JsonFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
        _editorChanging = false;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}

internal sealed class JsonFoldingStrategy
{
    public static void UpdateFoldings(FoldingManager manager, TextDocument document)
        => manager.UpdateFoldings(CreateFoldings(document.Text), -1);

    private static List<NewFolding> CreateFoldings(string text)
    {
        var result = new List<NewFolding>();
        var stack  = new Stack<int>();

        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '{' or '[':
                    stack.Push(i);
                    break;
                case '}' or ']' when stack.Count > 0:
                    var start = stack.Pop();
                    if (i > start + 1)
                        result.Add(new NewFolding(start, i + 1));
                    break;
            }
        }

        result.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return result;
    }
}
