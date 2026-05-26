using System.Windows.Controls;
using System.Windows.Input;
using D4LootBench.App.ViewModels;

namespace D4LootBench.App.Views;

public partial class AiAssistantView : UserControl
{
    public AiAssistantView()
    {
        InitializeComponent();
    }

    private void PromptBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            // Ctrl+Enter → insert newline at caret
            var tb    = (TextBox)sender;
            var caret = tb.CaretIndex;
            tb.Text       = tb.Text.Insert(caret, Environment.NewLine);
            tb.CaretIndex = caret + Environment.NewLine.Length;
        }
        else if (DataContext is AiAssistantViewModel vm &&
                 vm.GenerateRuleCommand.CanExecute(null))
        {
            vm.GenerateRuleCommand.Execute(null);
        }

        e.Handled = true;
    }

}
