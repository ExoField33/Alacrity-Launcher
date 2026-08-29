using System.Windows;

namespace Alacrity.Launcher;

public partial class SteamAccountPromptWindow : Window
{
    private SteamAccountPromptWindow(string message)
    {
        InitializeComponent();
        PromptText.Text = message;
        Loaded += (_, _) => AccountNameTextBox.Focus();
    }

    public string? AccountName { get; private set; }

    public static string? Show(Window owner, string message)
    {
        var dialog = new SteamAccountPromptWindow(message) {
            Owner = owner
        };
        return dialog.ShowDialog() == true ? dialog.AccountName : null;
    }

    private void Continue_Click(object sender, RoutedEventArgs eventArgs)
    {
        string accountName = AccountNameTextBox.Text.Trim();
        if (accountName.Length == 0) {
            return;
        }

        AccountName = accountName;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs eventArgs)
    {
        DialogResult = false;
    }
}
