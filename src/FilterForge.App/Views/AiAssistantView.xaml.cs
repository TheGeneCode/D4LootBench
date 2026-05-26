using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ThunderEagle.FilterForge.App.ViewModels;

namespace ThunderEagle.FilterForge.App.Views;

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

    // PasswordBox.Password has no dependency property, so binding isn't possible.
    // Push the value into the VM on every keystroke instead.
    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AiAssistantViewModel vm)
            vm.ApiKey = ((PasswordBox)sender).Password;
    }
}
